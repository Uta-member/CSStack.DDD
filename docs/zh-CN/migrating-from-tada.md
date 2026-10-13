# 从 CSStack.TADA 迁移

CSStack.DDD 是 CSStack.TADA 的后继。本指南介绍如何将使用 TADA 的项目迁移到 CSStack.DDD。

本指南以 TADA 4.0.0 为准。如果你使用的是更早的版本，请先阅读[从旧版本迁移](#从旧版本迁移)。迁移之前无需先升级到 TADA 4.0.0，因为不同 TADA 版本之间有差异的代码，本来就是要重写的代码。

## 开始之前

- **事务的表达方式正好相反。** 在 TADA 中，每次在 `ExecuteTransactionAsync` 内调用仓储都要传入会话。在 CSStack.DDD 中，仓储和工作单元在 DI 作用域内共享连接，由应用服务在最后提交。应用服务需要重写，而不只是改名，但重写后的代码会更短。
- **校验从异常改为错误。** `Validate()` 返回错误列表，由实体的操作抛出异常。请参阅[校验](validation.md)。
- **TADA 提供的部分内容不再提供。** 没有聚合服务、领域服务接口、命令服务与查询服务接口、`Optional<T>` 和事务管理器。原因请参阅[设计决策](design-decisions.md#本工具包不提供的内容)。
- **从下层开始逐层迁移。** 替换包之后，所有 `using CSStack.TADA;` 都会编译失败。先修改领域层，再依次修改应用层、基础设施层和组合根，每完成一层就构建一次。

## 变化概览

| 方面 | CSStack.TADA | CSStack.DDD |
|---|---|---|
| 事务 | `ITransactionManager.ExecuteTransactionAsync` 用 lambda 包裹用例并传入会话 | 仓储登记变更，`IUnitOfWork.CommitAsync` 提交 |
| 校验 | `Validate()`（或 `Create`）抛出异常 | `Validate()` 返回错误，操作抛出 `DomainValidationException` |
| 创建与重建 | 值对象和实体都有 `Create` 和 `Reconstruct` | 只在实体上提供。值对象只有一个构造函数 |
| 保存 | `SaveAsync` 是 upsert | `Save` 只做登记。插入还是更新，由基础设施层根据是否加载过来决定 |
| 未找到 | `Optional<T>.Empty` | `null` |
| 领域事件 | 不提供 | `AggregateRoot` 记录事件，工作单元在提交时取出 |
| 操作的执行者 | 传给 `SaveAsync` 的 `TOperateInfo` | 具有业务含义的“谁”作为操作的参数；用于审计的“谁”由基础设施层记录 |
| 并发控制 | 不提供 | 由基础设施层检测，并抛出 `ConcurrencyConflictException` |

## 类型对照表（TADA 4.0.0）

| CSStack.TADA | CSStack.DDD | 说明 |
|---|---|---|
| `using CSStack.TADA;` | `using CSStack.DDD.Domain;` / `using CSStack.DDD.Application;` | 每一层使用各自的命名空间 |
| `IValueObject` | `IValueObject<TSelf>` | record 会自动满足 |
| `IValueObject.Validate()`（`void`，抛出异常） | `IValidatable.Validate()`（返回 `IReadOnlyList<ValidationError>`） | `IValidatable` 是单独的接口 |
| `ISingleValueObject<TValue>` | `ISingleValueObject<TSelf, TValue>` | 在**最前面**增加 `TSelf` |
| `ISingleValueObject<TValue, TSelf>` | `ISingleValueObject<TSelf, TValue>` | 类型参数的顺序**相反**。不要求 `Create` 和 `Reconstruct` |
| `IEntity<TIdentifier>` / `EntityBase<TSelf, TIdentifier>` | `IEntity<TSelf, TId>` / `Entity<TSelf, TId>` | `TId` 必须实现 `IEquatable<TId>` |
| `Identifier` | `Id` | |
| `IEntity.Validate()` | 无 | 改为在操作中强制 |
| 无 | `AggregateRoot<TSelf, TId>`、`IDomainEvent`、`IDomainEventSource` | 如果自己编写过基类，用它们替换 |
| `IAggregateService<...>` / `AggregateServiceBase<...>` | 无 | 实体的操作，加上应用服务 |
| `IRepository<TEntity, TId, TOperateInfo, TSession>` | `IRepository<TAggregate, TId>` | 不再有会话和操作信息 |
| `IRepositoryDeletable<...>` | `IDeletableRepository<TAggregate, TId>` | |
| `FindByIdentifierAsync(session, id, ct)` → `ValueTask<Optional<T>>` | `FindAsync(id, ct)` → `Task<T?>` | |
| `SaveAsync(session, entity, operateInfo, ct)` | `Save(aggregate)` | 同步，只做登记 |
| `DeleteAsync(session, entity, operateInfo, ct)` | `Delete(aggregate)` | 同步，只做登记 |
| `ITransactionManager`、`TransactionManager`、`TransactionSessions` | `IUnitOfWork` | |
| `ITransactionService<TSession>` | 你对 `IUnitOfWork` 的实现 | 基础设施层 |
| 发生异常时回滚 | 不提交就释放（Dispose） | 没有回滚方法 |
| `ICommandService<...>`、`ICommandServiceDTO`、`IQueryService<...>`、`IQueryServiceDTO` | 无 | 在各自的接口中声明 `ExecuteAsync` |
| `Optional<T>`、`OptionalExtensions` | 无 | 参阅第 5 步 |
| `TADAException`、`NestedTransactionException`、`TransactionSessionNotFoundException` | 无 | |
| 无 | `ValidationError`、`Required`、`InvalidFormat`、`TooLong`、`TooShort`、`BelowMinimum`、`AboveMaximum`、`DomainException`、`DomainValidationException` | |
| 无 | `GetAsync`（`IRepository` 的扩展方法）、`AggregateNotFoundException`、`ConcurrencyConflictException` | |

## 第 1 步：替换包

```xml
<!-- 迁移前 -->
<PackageReference Include="CSStack.TADA" Version="4.0.0" />
<!-- 迁移后 -->
<PackageReference Include="CSStack.DDD" Version="x.y.z" />
```

然后，将领域层中的 `using CSStack.TADA;` 替换为 `using CSStack.DDD.Domain;`。只在使用工作单元的地方添加 `using CSStack.DDD.Application;`。

## 第 2 步：值对象

```csharp
// 迁移前（TADA）
public sealed record Email : ISingleValueObject<string, Email>
{
	public string Value { get; }

	private Email(string value)
	{
		Value = value;
	}

	public static Email Create(string value)
	{
		var email = new Email(value);
		email.Validate();
		return email;
	}

	public static Email Reconstruct(string value) => new(value);

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) throw new ArgumentException("The email address is required.");
		if (!Value.Contains("@")) throw new ArgumentException("The email address is invalid.");
	}
}

// 迁移后（CSStack.DDD）
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value));
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

- 交换类型参数：`ISingleValueObject<string, Email>` 变为 `ISingleValueObject<Email, string>`。
- 同时声明 `IValidatable`。`ISingleValueObject` 不包含它。
- 返回错误，而不是抛出异常。选择能描述问题的错误类型（`Required`、`TooLong` 等），或从 `ValidationError` 派生自己的类型。
- 将 `Email.Create(x)` 和 `Email.Reconstruct(x)` 都替换为 `new Email(x)`。创建值时不再进行校验。校验由实体在用该值创建或修改实体时进行（第 3 步）。请确认原来调用 `Create` 的每个地方，现在都会把值传给这样的操作。
- 不使用构造函数、改用 `required` 的 `init` 属性，同样满足 `Value { get; }`。如果为了不修改调用方而保留这种写法，由于没有构造函数来拒绝 `null`，请在 `Validate()` 中将 `null` 报告为 `Required`。

请参阅[值对象](value-objects.md)。

## 第 3 步：实体与聚合根

```csharp
// 迁移前（TADA）
public sealed class Member : EntityBase<Member, MemberId>
{
	public override MemberId Identifier { get; }
	public Email Email { get; private set; }

	private Member(MemberId identifier, Email email)
	{
		Identifier = identifier;
		Email = email;
	}

	public static Member Create(MemberId identifier, Email email)
	{
		var member = new Member(identifier, email);
		member.Validate();
		return member;
	}

	public static Member Reconstruct(MemberId identifier, Email email) => new(identifier, email);

	public void ChangeEmail(Email email)
	{
		email.Validate();
		Email = email;
	}

	public override void Validate()
	{
		Email.Validate();
	}
}

// 迁移后（CSStack.DDD）
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	public static Member Register(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid(); // 在创建之前校验
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(occurredAt, member.Id, email));
		return member;
	}

	public static Member Reconstruct(MemberId id, Email email) => new(id, email);

	public void ChangeEmail(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		Email = email;
		AddDomainEvent(new MemberEmailChanged(occurredAt, Id, email));
	}
}
```

- 聚合根从 `AggregateRoot<TSelf, TId>` 派生，聚合内部的实体从 `Entity<TSelf, TId>` 派生。
- 将 `Identifier` 重命名为 `Id`。如果实体有一个带业务名称的属性（例如 `MemberId`），可以保留它，并写成 `public override MemberId Id => MemberId;`。
- `TId` 必须实现 `IEquatable<TId>`。用 record 实现的标识符已经实现了它。
- 删除公开的 `Validate()`。在每个操作中校验输入；对于多个输入，先用 `Under` 收集错误，再调用一次 `ThrowIfInvalid()`。请参阅[校验](validation.md#关卡)。
- 在创建实体**之前**校验，而不是之后。
- 如果你曾用 `Validate()` 检查重建后的数据，请在实体上实现 `IValidatable`，返回错误而不是抛出异常。
- `Reconstruct` 仍然不做校验，也不记录事件。

请参阅[实体与聚合](entities-and-aggregates.md)。

## 第 4 步：领域事件

TADA 不提供领域事件。如果你自己编写了聚合根基类和事件接口，请删除它们，改用 `AggregateRoot<TSelf, TId>` 和 `IDomainEvent`。

- `IDomainEvent.OccurredAt` 的类型是 `DateTimeOffset`。`DateTime` 可以隐式转换，但 `Kind` 为 `Local` 或 `Unspecified` 的 `DateTime` 会带上本地时区偏移。请确保传入的时间是 UTC，或者改为 `DateTimeOffset`。
- 不要在应用服务中清除事件。删除诸如先调用 `outbox.AddAsync(session, aggregate.DomainEvents)`、再调用 `ClearDomainEvents()` 的代码。工作单元会在提交时通过 `PullDomainEvents()` 取出事件（第 9 步）。
- `DomainEvents` 是只读视图。调用 `PullDomainEvents()` 之后它会变为空，如果需要保留事件，请复制一份。

请参阅[领域事件](domain-events.md)。

## 第 5 步：不使用 `Optional<T>` 的部分更新

CSStack.DDD 没有 `Optional<T>`。原来使用它的地方，替换方式取决于它表示的含义。

| 位置 | 替换方式 |
|---|---|
| `FindByIdentifierAsync` 的返回值 | `FindAsync` 返回 `null` |
| 接收每个字段都是 `Optional<T>` 的请求的 `Update(UpdateReq)` 操作（“未指定”表示“不修改”） | 拆分为每个字段一个操作（`ChangeName`、`ChangeEmail` 等） |
| 在一个事务中修改多个字段的命令的请求 DTO | 自己定义一个具有三种状态的类型，只在应用层使用 |

**不要用可空类型（`string?`）替换 `Optional<T>`。** 在没有属性接受 `null` 时它还能工作，但一旦出现这样的属性，就无法区分“修改为 `null`”和“不修改”。

拆分操作符合[意图优先于覆盖](entities-and-aggregates.md#操作)这一准则。调用操作本身就表示“要修改”，因此参数不需要“未指定”的状态。

```csharp
// 迁移前（TADA）
public void Update(UpdateReq req) // UpdateReq { Optional<MemberName> NameOptional; Optional<Email> EmailOptional; }

// 迁移后
public void ChangeName(MemberName name, DateTimeOffset occurredAt)
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
```

如果仍然需要用一次请求修改多个字段的命令，就在它的请求 DTO 中保留具有三种状态的类型，只调用已指定字段对应的操作。不要在领域层中使用该类型。

```csharp
if (req.Name.TryGetValue(out var name))
{
	member.ChangeName(new MemberName(name), req.OccurredAt);
}
if (req.Email.TryGetValue(out var email))
{
	member.ChangeEmail(new Email(email), req.OccurredAt);
}
members.Save(member);
await unitOfWork.CommitAsync(cancellationToken);
```

通常，一个具有 `HasValue`、`TryGetValue` 以及从 `T` 隐式转换的小型 `readonly struct` 就足够了。请注意以下权衡：

- 不再是整个更新对应一个事件，而是每个被修改的字段各记录一个事件。
- 如果多个操作都校验失败，只会报告第一个操作的错误。如果要报告全部错误，请先校验请求。请参阅[使用表单中的名称校验输入](validation.md#使用表单中的名称校验输入)。
- 如果后面的操作抛出异常，什么都不会提交，因此不会出现只修改了部分字段的情况。

## 第 6 步：仓储

```csharp
// 迁移前（TADA）
public interface IMemberRepository<TSession> : IRepository<Member, MemberId, OperateInfo, TSession>
	where TSession : IDisposable
{
}

// 迁移后（CSStack.DDD）
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

- 会话和操作信息的类型参数不再存在，因此使用仓储的服务中的 `<TSession>` 也会消失。
- 仓储可以包含领域所需的查询，例如 `FindByEmailAsync`。返回值与 `FindAsync` 保持一致，使用 `Task`。
- 只有真正会消失的聚合才从 `IDeletableRepository` 派生。

聚合服务（`IAggregateService` 和 `AggregateServiceBase`）没有对应的类型。将“获取、修改、保存”移到实体的操作和应用服务中（第 7 步）。将 `GetEntityByIdentifierAsync` 替换为 `FindAsync`。

请参阅[仓储与工作单元](repositories-and-unit-of-work.md)。

## 第 7 步：应用服务

这是代码变化最大的部分。

```csharp
// 迁移前（TADA）
public sealed class ChangeMemberEmailCommandService<TMemberSession>(
	ITransactionManager transactionManager,
	IMemberRepository<TMemberSession> members) : IChangeMemberEmailCommandService
	where TMemberSession : IDisposable
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		await transactionManager.ExecuteTransactionAsync<TMemberSession>(
			async (sessions, ct) =>
			{
				var session = sessions.GetSession<TMemberSession>();
				var found = await members.FindByIdentifierAsync(session, req.MemberId, ct);
				if (!found.TryGetValue(out var member))
				{
					throw new MemberNotFoundException(req.MemberId);
				}

				member.ChangeEmail(Email.Create(req.Email));
				await members.SaveAsync(session, member, req.OperateInfo, ct);
			},
			cancellationToken: cancellationToken);
	}
}

// 迁移后（CSStack.DDD）
public sealed class ChangeMemberEmailCommandService(
	IMemberRepository members,
	IUnitOfWork unitOfWork) : IChangeMemberEmailCommandService
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		var member = await members.GetAsync(req.MemberId, cancellationToken);

		member.ChangeEmail(new Email(req.Email), req.OccurredAt);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- lambda、会话和 `<TSession>` 类型参数都不再需要。
- 如果抛出异常，就不会执行到 `CommitAsync`，工作单元会随 DI 作用域一起被释放。已登记的变更会被丢弃，这取代了 TADA 的回滚。原来写在 `beforeRollbackHandler` 中的逻辑，移到应用服务中的 `try`/`catch`，或全局的异常处理中。
- **不要在一个应用服务中调用另一个应用服务。** TADA 会用 `NestedTransactionException` 拒绝这种做法。在 CSStack.DDD 中它可以编译和运行，但内层的 `CommitAsync` 会在外层用例的中途提交。请将共用的逻辑移到领域服务或实体中。

### 领域服务

TADA 在 3.x 及之前提供 `IDomainService<TReq>`，会话放在其请求 DTO 中。在 CSStack.DDD 中，领域服务是不持有会话的普通类（如有需要也可以定义接口）。它不提交。加载、保存和提交由应用服务完成。如果领域服务需要查找某些内容，例如确认某个电子邮件地址尚未被使用，可以接收仓储接口。

### 多个数据存储

TADA 通过会话类型区分数据存储（`ExecuteTransactionAsync<TUserSession, TOrderSession>`）。`IUnitOfWork` 只有一种类型，因此请在应用层为每个数据存储派生一个接口。

```csharp
public interface IMemberUnitOfWork : IUnitOfWork { }
public interface IOrderUnitOfWork : IUnitOfWork { }
```

如果只有一个数据存储，直接使用 `IUnitOfWork` 即可。提交两个工作单元并不是原子的，这与在 TADA 中提交两个会话相同。请优先让每个用例只修改一个数据存储，并通过领域事件影响另一个数据存储。

### 事件处理器

如果你的处理器曾通过 `ExecuteTransactionAsync` 自行开启事务，现在改为登记变更，并在其自身作用域的工作单元上调用 `CommitAsync`。请参阅[对事件作出响应](domain-events.md#对事件作出响应)。

## 第 8 步：操作的执行者

TADA 将 `TOperateInfo` 传给 `SaveAsync` 和 `DeleteAsync`，通常用于填写审计列。CSStack.DDD 区分两种“谁”（[操作的执行者](entities-and-aggregates.md#操作的执行者)）。

- **具有业务含义的“谁”**（作者、审批人）：作为操作的参数传入。为此，你可以在请求 DTO 中保留 `OperateInfo`。
- **用于审计的“谁”**（`UpdatedBy`、`UpdatedAt`）：由基础设施层记录。在请求管道或命令行等入口处设置执行上下文（例如，一个保存用户和时间的作用域对象），由仓储和工作单元读取它。领域层和应用层不读取它。

如果通过发件箱在另一个作用域中分发事件，请将操作者写入发件箱记录的信封，并在运行处理器的作用域中据此恢复执行上下文（[事件元数据](domain-events.md#事件元数据)）。

## 第 9 步：基础设施层

将 `ITransactionService<TSession>` 的实现替换为 `IUnitOfWork` 的实现。所需的内容请参阅[实现基础设施层](repositories-and-unit-of-work.md#实现基础设施层)。与 TADA 不同的地方如下：

| 方面 | TADA | CSStack.DDD |
|---|---|---|
| 开启事务 | 在执行 lambda 之前调用 `BeginAsync` | 在首次访问数据库时开启，使读取和写入处于同一事务中 |
| 插入还是更新 | `SaveAsync` 是 upsert | 记住加载过哪些聚合（标识映射）。**不要使用 upsert 语句**，它们会悄无声息地覆盖已有数据 |
| 领域事件 | 由应用服务写入 | 在 `CommitAsync` 中从已登记的聚合取出，并写入发件箱 |
| 回滚 | `RollbackAsync` | 不提交就释放 |
| 并发控制 | 不提供 | 在聚合根上使用乐观并发控制，发生冲突时抛出 `ConcurrencyConflictException` |

按如下方式向 DI 容器注册服务。

- 由于仓储依赖作用域中的工作单元，工作单元和仓储都注册为**作用域**生命周期。在 TADA 中注册为单例的仓储，必须改为作用域。设置 `ValidateScopes = true` 可以发现遗漏的注册。
- 只注册一次实现，并将各个接口转发给它，使应用层和仓储在同一作用域内共享同一个实例。

```csharp
services.AddScoped<MemberUnitOfWork>();
services.AddScoped<IMemberUnitOfWork>(sp => sp.GetRequiredService<MemberUnitOfWork>());
services.AddScoped<IMemberRepository, MemberRepository>(); // 依赖 MemberUnitOfWork
```

- 删除 `TransactionManager` 和 `ITransactionService<TSession>` 的注册。

## 第 10 步：命令服务与查询服务的接口

CSStack.DDD 没有 `ICommandService`、`IQueryService` 及其 DTO 标记接口。请在各个接口中声明 `ExecuteAsync`。请求 DTO 和响应 DTO 可以继续嵌套在接口中。

```csharp
// 迁移前（TADA）
public interface IChangeMemberEmailCommandService : ICommandService<IChangeMemberEmailCommandService.Req>
{
	public sealed record Req : ICommandServiceDTO { ... }
}

// 迁移后
public interface IChangeMemberEmailCommandService
{
	ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);

	public sealed record Req { ... }
}
```

如果你的代码依赖这些公共接口（例如用作泛型约束），请在应用程序中定义相同形式的接口。

## 第 11 步：异常

| CSStack.TADA | CSStack.DDD |
|---|---|
| `Validate()` 或 `Create` 抛出的异常 | `DomainValidationException`，其 `Errors` 包含带路径的强类型错误 |
| `ValueObjectNullException`、`ValueObjectLengthException`、`ValueObjectInvalidException`（3.x 及之前） | 由 `Validate()` 报告的 `Required`、`TooShort`/`TooLong`、`InvalidFormat` |
| `ObjectNotFoundException`（3.x 及之前） | 由 `GetAsync` 抛出的 `AggregateNotFoundException` |
| `ObjectAlreadyExistException`、`DomainInvalidOperationException`（3.x 及之前） | 你自己的异常。业务规则的异常派生自 `DomainException` |
| `NestedTransactionException`、`TransactionSessionNotFoundException` | 无 |
| 无 | `ConcurrencyConflictException` |

在表示层统一处理一次 `DomainException`（包括 `DomainValidationException`）、`AggregateNotFoundException` 和 `ConcurrencyConflictException`。错误不包含消息，因此也在那里将其转换为消息。请参阅[将错误转换为消息](validation.md#将错误转换为消息)。

## 能够编译但行为不同的变化

| 变化 | 影响 |
|---|---|
| `Save` 不是 upsert | 保存未加载的聚合时，如果已存在相同标识符的行，会因主键冲突而失败，而不是覆盖它 |
| 实体相等性使用 `is TSelf` 判断 | 在 TADA 3.0 及之后，运行时类型不同的实体永远不相等。在 CSStack.DDD 中，从同一个 `TSelf` 派生的两个类，只要标识符相等就相等。哈希码不再包含类型 |
| `FindAsync` 返回 `null` | 原来检查 `HasValue` 的代码需要改为检查 `null` |
| 校验异常 | `ArgumentException` 等异常变为 `DomainValidationException` |
| `OccurredAt` 是 `DateTimeOffset` | 本地或未指定的 `DateTime` 会带上本地时区偏移 |
| 嵌套的用例 | 不再被拒绝，内层用例会在中途提交 |

## 从旧版本迁移

仓储、事务和实体的形式在所有 TADA 版本中都相同，因此第 1 步以及第 3 至 11 步可以直接适用。差异主要在校验方面。

请查看包含你所用版本的行。

| TADA 版本 | 与 4.0.0 的差异 | 处理方式 |
|---|---|---|
| 2.0.1 至 3.0.x | `IValueObject` 和 `IEntity` 没有 `Validate()`。规则在 `Create` 中检查 | 将 `Create` 中的检查移到 `Validate()` 中，并改为返回错误（第 2 步） |
| 3.0.x 及之前 | 存在 `IDomainService<TReq>`、`IDomainServiceDTO`、`ILengthDefinedSingleValueObject` 以及第 11 步中的异常 | 领域服务请参阅[领域服务](#领域服务)。长度的上下限可以保留为值对象的 `static` 成员，或作为 `Validate()` 中使用的常量 |
| 2.0.x 及之前 | `ExecuteTransactionAsync` 的 lambda 不接收 `CancellationToken`，实体相等性不比较运行时类型 | lambda 在第 7 步中会被去掉，因此第一个差异没有影响。对于派生类型，CSStack.DDD 的相等性与这些版本的行为相同 |
| 1.x 和 0.x | `ValueObjectBase`（带有 `Validate()` 和 `IsInvalidValue` 的抽象 record）、`IEntity.IsInvalidValue`、`ValidateHelper`、`KeyedValidateHelper<TKey>`、`MultiReasonException`、`KeyedMultiReasonException<TKey>`，以及 `ICommandServiceWithRes`、`IQueryServiceWithoutReq`、`IDomainServiceWithRes` | 不再从 `ValueObjectBase` 派生，改为实现 `IValueObject<TSelf>` 和 `IValidatable`。将 `IsInvalidValue` 替换为 `Validate().Count > 0`。不再使用这些辅助类，改为将 `ValidationError` 收集到列表中；原来使用键的地方改用 `Under`。`WithRes` 和 `WithoutReq` 接口按第 10 步的方式替换 |
| 1.x 和 0.x | 没有 `ISingleValueObject<TValue>`（只有 `ISingleValueObject<TValue, TSelf>`）。1.0.4 之前也没有 `IRepositoryDeletable` | 无需额外处理。使用 `ISingleValueObject<TSelf, TValue>`，会被删除的聚合使用 `IDeletableRepository` |

## 检查清单

- [ ] 已替换包和命名空间，没有任何地方引用 `CSStack.TADA`。
- [ ] 值对象通过 `Validate()` 报告错误，构造函数只检查该类型存在所必需的条件。
- [ ] 每个创建或修改实体的操作都用 `ThrowIfInvalid()` 校验输入。
- [ ] `Reconstruct` 既不校验也不记录事件。
- [ ] 在 `null` 是合法值的地方，没有用可空类型替换 `Optional<T>`。
- [ ] 应用服务调用 `Save` 和 `CommitAsync`，并且不调用其他应用服务。
- [ ] 工作单元在提交时取出领域事件，应用层不清除它们。
- [ ] 审计列由基础设施层根据执行上下文填写。
- [ ] 仓储根据是否加载过来决定插入还是更新，不使用 upsert 语句。
- [ ] 工作单元和仓储注册为作用域生命周期。
- [ ] 在表示层处理 `DomainException`（包括 `DomainValidationException`）、`AggregateNotFoundException` 和 `ConcurrencyConflictException`。
