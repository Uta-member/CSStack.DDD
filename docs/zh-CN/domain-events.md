# 领域事件

领域事件表示业务上有意义的某件事已经发生这一事实，例如“会员已注册”。

## 定义事件

```csharp
public sealed record MemberRegistered(DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : DomainEvent(OccurredAt);
```

`DomainEvent` 实现 `IDomainEvent`，默认通过 `Guid.NewGuid()` 生成 `EventId`。请显式传入 `OccurredAt`。从持久化存储恢复事件时，通过对象初始化器将 `EventId` 设为已保存的标识符；`with` 复制也会保留该标识符。如果需要其他表示方式，仍可直接实现 `IDomainEvent`。

| 属性 | 用途 |
|---|---|
| `EventId` | 唯一标识符。消费方用它来忽略重复投递。 |
| `OccurredAt` | 事件发生的时间。 |
| 聚合的标识符 | 事件发生在哪个聚合中。 |
| 业务数据 | 消费方响应该事件所需的信息。 |

指导原则：

- **用过去时为事件命名**，例如 `MemberRegistered` 或 `EmailChanged`。
- **将事件设计为不可变的 record。** 事实一旦发生就不会改变。
- **不要引用聚合本身。** 聚合是可变的，因此应复制消费方需要的值。
- **以参数接收发生时间**，而不是在领域内部读取时钟。时间由调用方（通常是应用层）决定，因此领域的结果不受执行时刻影响、易于测试，也可以记录过去发生的事实（例如导入数据时）。

## 记录事件

派生自 `AggregateRoot<TSelf, TId>` 的聚合根使用 `AddDomainEvent` 记录事件。

```csharp
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
{
	email.Validate().Under(nameof(Email)).ThrowIfInvalid();
	Email = email;
	AddDomainEvent(new MemberEmailChanged(occurredAt, Id, email));
}
```

不要在重建用的工厂方法中记录事件。重建只是恢复过去的状态，并没有发生任何新的事情。

## 为什么不立即分发事件

事件先记录在聚合中，等到提交变更时再分发。

1. 如果操作之后事务失败，立即分发的事件描述的将是一个从未真正发生的变更。
2. 聚合无需依赖分发器，只需记录发生了什么。

## 收集事件

基础设施层通过调用 `PullDomainEvents()`，从所有已登记到工作单元的聚合中收集事件。

```csharp
foreach (var aggregate in registeredAggregates)
{
	if (aggregate is IDomainEventSource source)
	{
		foreach (var domainEvent in source.PullDomainEvents())
		{
			// 写入发件箱
		}
	}
}
```

- `IDomainEventSource` 是非泛型接口，因此无需反射即可从任意类型的聚合中收集事件。
- `PullDomainEvents()` 在一次操作中返回事件并将其清空，因此同一事件不会因失误被分发两次。
- `AggregateRoot<TSelf, TId>` 显式实现 `PullDomainEvents()`，因此只能通过 `IDomainEventSource` 调用，而不能在聚合本身上调用。这样可以防止应用服务或领域代码在提交前取出事件，导致事件被清空而无法写入发件箱。如需检查某个操作产生了哪些事件（例如在测试中），请读取不会清空事件的 `DomainEvents`。

## 何时分发

| 时机 | 适用场景 | 注意事项 |
|---|---|---|
| **发件箱模式（Outbox，推荐）** | 更新其他聚合（最终一致性）、发送邮件等副作用、与其他系统集成 | 投递语义为至少一次，消费方必须忽略重复投递 |
| 提交前，在同一事务中 | 业务要求立即一致，并有意在一个事务中修改多个聚合的情况 | 响应失败时，原始变更也会被回滚。事务会变大 |
| 提交后直接分发 | 不推荐 | 如果进程在分发前停止，事件会丢失 |

默认采用发件箱模式。处理过程分为两个事务。

```
事务 1（用例）
  更新聚合 + 将事件写入发件箱 → 提交
  ※ 此时尚未对事件作出响应

事务 2（发件箱发布进程，按事件逐个处理）
  从发件箱取出事件 → 对事件作出响应 → 响应产生的变更 + 已处理标记 → 提交
  ※ 失败时回滚并重试，不影响事务 1 的变更
```

即使响应失败，原始用例的变更也不会被撤销。响应产生的变更会通过重试在之后生效（最终一致性）。

在事务内作出响应时，其中抛出的异常会连同原始变更一起回滚。例如，开设积分账户失败时，会员注册也会失败。只有在业务确实需要时，才应有意选择这种方式。

## 在工作单元中写入发件箱

`CommitAsync` 从已登记的聚合中收集事件，并在与聚合相同的事务中将其写入发件箱。

```csharp
public async Task CommitAsync(CancellationToken cancellationToken = default)
{
	var transaction = await GetTransactionAsync(cancellationToken);

	var domainEvents = _registeredAggregates.SelectMany(aggregate => aggregate.PullDomainEvents()).ToList();

	foreach (var work in _pending) await work(transaction, cancellationToken);
	await WriteOutboxAsync(domainEvents, transaction, cancellationToken); // 在与聚合相同的事务中写入
	cancellationToken.ThrowIfCancellationRequested();
	transaction.Commit();
}
```

- `_registeredAggregates` 是 `List<IDomainEventSource>`。
- 如果事务已回滚，聚合的变更和事件都不会被持久化。请释放失败的工作单元，并丢弃其聚合实例：`PullDomainEvents()` 已经清除了这些实例的事件。对于可以安全重试的操作，请创建新的作用域，从加载开始重新执行整个用例。不要只重试 `CommitAsync`，也不要在另一个工作单元中保存同一聚合实例。如果提交结果未知，例如连接中断后，请先确认持久化结果，或通过应用层的幂等机制保证安全，再进行重试。

## 对事件作出响应

工具包不提供用于响应事件的处理器接口，也不提供解析并调用处理器的分发器。处理器按什么单位调用、在哪个事务中运行、如何重试，取决于应用程序的构成。请使用自己实现的机制，或 MediatR、Wolverine、MassTransit 等库。

无论使用哪种机制，都请遵守以下几点：

- **监视发件箱并分发事件的是基础设施层的发件箱中继（Outbox Relay）。** 由宿主以 `BackgroundService` 或独立的 Worker 进程的形式常驻运行。分发事件的既不是保存聚合的应用服务，也不是表示层。
- **针对每个事件，在与原始变更分开的作用域、工作单元和事务中作出响应。** 响应产生的变更与“已处理”标记在同一事务中提交。
- **使响应具有幂等性。** 事件至少会投递一次。与已处理标记在同一事务中提交的数据库变更不会被重复应用，但按业务键检查是否已存在，并以唯一约束作为最后一道防线，会更加安全。发送邮件等外部副作用在之后的提交失败时会被重复执行，因此请按 `EventId` 忽略重复。
- **失败时重试，超过上限后移入死信。**

下面的示意代码展示了发件箱中继需要实现的部分，并不是完整的实现。

```csharp
internal sealed class OutboxRelay(IServiceScopeFactory scopes, OutboxStore outbox) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			foreach (var record in await outbox.FetchPendingAsync(100, stoppingToken))
			{
				try
				{
					// 为每个事件创建作用域，因此工作单元和事务也与原始变更分开
					using var scope = scopes.CreateScope();
					var unitOfWork = scope.ServiceProvider.GetRequiredService<DapperUnitOfWork>();

					await ReactAsync(scope.ServiceProvider, record.Deserialize(), stoppingToken); // 如何调用处理器由应用程序决定
					unitOfWork.Enqueue((transaction, cancellationToken) => outbox.MarkProcessedAsync(record.Id, transaction, cancellationToken));
					await unitOfWork.CommitAsync(stoppingToken); // 将响应产生的变更与已处理标记一起提交
				}
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					await outbox.RecordFailureAsync(record.Id, exception, stoppingToken); // 增加尝试次数，超过上限后移入死信
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}
}
```

- 响应中保存其他聚合时，其事件会由同一次 `CommitAsync` 写入发件箱，并在之后的轮次中分发。事件链也会一步一步地、各自在独立的事务中处理。
- 如果一个事件有多个处理器且它们共享一个事务，其中一个失败时，所有处理器都会回滚并一起重试。如果希望处理器彼此独立，请按处理器（订阅者）拆分发件箱记录，或在消息代理中为每个订阅者使用单独的队列。
- 运行多个中继实例时，请在取出记录时加锁，以免同一条记录被重复处理（`SELECT ... FOR UPDATE SKIP LOCKED`，SQL Server 中为 `WITH (UPDLOCK, READPAST)`）。
- 使用消息代理时，中继只负责把发件箱记录发送到代理。由从代理接收消息的消费者按同样的步骤（创建作用域、作出响应、提交）作出响应。

### 在事务内作出响应（需要立即一致时）

当业务要求立即一致时，也可以在 `CommitAsync` 中、提交之前对事件作出响应。

- 响应中保存其他聚合会产生新的事件。请重复收集和响应，直到不再有事件为止，并限制轮次，以免事件相互触发而无限循环。
- 响应抛出异常时，原始变更也会被回滚。
- 这里只应处理需要立即一致的响应。以最终一致性处理的部分，仍然写入发件箱。

## 事件元数据

诸如由谁执行了操作、由哪个请求引发等信息属于元数据，而不是业务事实的一部分。请在写入发件箱时将其放入信封（envelope），而不是添加到每个事件中。

```
发件箱记录
├─ 信封（由基础设施层添加）：EventType、ActorId、CorrelationId、...
└─ 正文（由领域创建）：MemberRegistered { MemberId, Email, ... }
```

如果“谁”具有业务含义（例如审批人），则应将其包含在事件本身中。请参阅[实体与聚合](entities-and-aggregates.md)。

## 领域事件与集成事件

领域事件在同一进程内使用，因此可以包含值对象。而发送给其他系统的事件是与这些系统之间的契约。请将其转换为只包含基元值的独立集成事件类型，这样领域模型的变化就不会破坏契约。

## 多个聚合

一条常见的指导原则是：**每个事务只修改一个聚合**，其他聚合则通过领域事件来更新（最终一致性）。此时，更新其他聚合的处理器从发件箱运行，处于与原始变更分开的事务中（参阅[何时分发](#何时分发)）。借助工作单元，也可以在一个事务中修改多个聚合；当业务要求立即一致时，这是一个合理的选择，但应当是经过深思熟虑后的决定。在这种情况下，任何一个聚合的变更失败，所有变更都会被回滚。
