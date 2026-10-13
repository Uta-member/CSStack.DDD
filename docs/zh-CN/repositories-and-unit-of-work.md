# 仓储与工作单元

## 概述

| 类型 | 层 | 职责 |
|---|---|---|
| `IRepository<TAggregate, TId>` | 领域层 | 查找聚合，并登记待保存的聚合。 |
| `IDeletableRepository<TAggregate, TId>` | 领域层 | 此外还可登记待删除的聚合。 |
| `IUnitOfWork` | 应用层 | 在一个事务中提交所有已登记的变更。 |
| `ConcurrencyConflictException` | 应用层 | 报告另一个事务抢先修改了某个聚合。 |

领域并不知道事务的存在。事务是用例的边界，属于应用层关心的问题。

## 仓储

```csharp
public interface IRepository<TAggregate, TId>
{
	Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken = default);
	void Save(TAggregate aggregate);
}

public interface IDeletableRepository<TAggregate, TId> : IRepository<TAggregate, TId>
{
	void Delete(TAggregate aggregate);
}
```

- 遵循 .NET 中 `Find` 的惯例，聚合不存在时 `FindAsync` 返回 `null`。
- `Save` 和 `Delete` **只负责登记**聚合。在调用 `IUnitOfWork.CommitAsync` 之前不会写入任何内容。这也是它们是同步方法的原因。
- `Save` 不区分插入和更新，由实现来决定（见下文）。

为每个聚合定义一个仓储接口，并添加领域所需的查询。不要暴露 `IQueryable`，否则查询逻辑会泄漏到仓储之外。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

### 保存还是删除

只有对真正会消失的聚合才实现 `IDeletableRepository`。聚合是否拥有 `Delete` 是领域层的决定，并且会体现在接口上。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId> { }   // 退会是一种状态迁移
public interface ICartRepository : IDeletableRepository<Cart, CartId> { } // 购物车会真正消失
```

如果出于审计目的希望保留已删除的行，请在基础设施层中将 `Delete` 实现为软删除。从领域的角度看，该聚合已经不存在了。

## 工作单元

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork, TimeProvider time)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.FindAsync(new MemberId(memberId), cancellationToken)
			?? throw new KeyNotFoundException($"Member {memberId} was not found.");

		member.ChangeEmail(new Email(email), time);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- 在同一个 DI 作用域（每个请求一个）内，仓储与工作单元共享同一个连接。
- 没有回滚方法。如果工作单元在未提交的情况下被释放，变更就会被丢弃。
- 领域服务不负责提交。它们只操作接收到的聚合，加载、保存和提交由应用服务负责。

## 实现基础设施层

### 插入还是更新

实现通过记住哪些聚合是已加载的（标识映射，Identity Map）来决定执行插入还是更新。

```
FindAsync(id)   → 将该聚合记为“已加载”
Save(aggregate) → 已加载：UPDATE，否则：INSERT
```

如果待插入的聚合已经存在，主键约束会拒绝它。**不要使用数据库的 upsert 语句**，例如 `MERGE` 或 `ON CONFLICT DO UPDATE`，它们会悄无声息地覆盖已有数据。

### 在首次访问时开启事务

为了让读取和写入处于同一个事务中，请在首次访问数据库时（通常是在 `FindAsync` 中）开启事务，而不是在 `CommitAsync` 中。

### 并发控制

仅仅把用例包裹在事务中，**并不足以**防止并发修改。在默认隔离级别（READ COMMITTED）下，两个事务可能读取到相同的状态，都通过了聚合的检查，然后都执行写入。例如，如果两个事务各自向同一订单添加一行明细，两次插入都会成功，订单总额就可能超出上限。

常用的做法是**在聚合根上使用乐观锁**：

```
Save(order)
  UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @TokenWhenLoaded
    → 1 行：继续处理子行
    → 0 行：另一个事务抢先更新了 → 抛出 ConcurrencyConflictException
```

- **始终更新根所在的行**，即使只有子行发生了变化。这样，对聚合的任何变更都会在同一行上发生冲突。
- 将令牌保存在基础设施层中（放在标识映射中，或作为 EF Core 的影子属性）。领域不需要 `Version` 属性。
- 对于冲突频繁的聚合（库存、座位预订），可以考虑改为在 `FindAsync` 中使用悲观锁（`SELECT ... FOR UPDATE`，或 SQL Server 中的 `WITH (UPDLOCK)`）。

当抛出 `ConcurrencyConflictException` 时：

- 对于用户发起的操作，返回 HTTP 409 并请用户重新加载。
- 对于可以安全重复执行的操作（批处理、事件处理器），从加载开始重试整个用例，并限制重试次数。

### 跨聚合的规则

跨聚合的规则，例如“会员之间的电子邮件地址必须唯一”，无法由任何单个聚合的并发令牌来保护。请务必在数据库中添加唯一约束，作为最后一道防线。领域中的检查仅用于返回友好的错误信息。

### 收集领域事件

在 `CommitAsync` 中，对每个已登记的聚合调用 `PullDomainEvents()`，并在与聚合相同的事务中将事件写入发件箱。不要在这里对事件作出响应。由发件箱发布进程在与原始变更分开的事务中作出响应。仅当业务要求立即一致时，也可以在提交前作出响应。请参阅[领域事件](domain-events.md#何时分发)。

### 示例：Dapper

以下代码草图展示了实现所需的各个部分，并非完整实现。

```csharp
internal sealed class DapperUnitOfWork(DbConnection connection) : IUnitOfWork, IAsyncDisposable
{
	private DbTransaction? _transaction;
	private readonly Dictionary<object, long> _loaded = new(ReferenceEqualityComparer.Instance); // 聚合 → 令牌
	private readonly List<Func<DbTransaction, CancellationToken, Task>> _pending = [];

	internal async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken)
	{
		if (_transaction is not null) return _transaction;
		await connection.OpenAsync(cancellationToken);
		return _transaction = await connection.BeginTransactionAsync(cancellationToken);
	}

	internal void MarkLoaded(object aggregate, long token) => _loaded[aggregate] = token;
	internal bool TryGetToken(object aggregate, out long token) => _loaded.TryGetValue(aggregate, out token);
	internal void Enqueue(Func<DbTransaction, CancellationToken, Task> work) => _pending.Add(work);

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		var transaction = await GetTransactionAsync(cancellationToken);
		foreach (var work in _pending) await work(transaction, cancellationToken);
		// 在此处从已登记的聚合中取出领域事件，并写入发件箱（参阅领域事件）
		await transaction.CommitAsync(cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is not null) await _transaction.DisposeAsync(); // 未提交时会回滚
		await connection.DisposeAsync();
	}
}
```

```csharp
public void Save(Order order) => _unitOfWork.Enqueue(async (transaction, cancellationToken) =>
{
	var connection = transaction.Connection!;
	if (_unitOfWork.TryGetToken(order, out var token))
	{
		var affected = await connection.ExecuteAsync(
			"UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @Token",
			new { Id = order.Id.Value, Token = token }, transaction);
		if (affected == 0)
		{
			throw new ConcurrencyConflictException(typeof(Order), order.Id);
		}
	}
	else
	{
		await connection.ExecuteAsync("INSERT INTO Orders (..., Token) VALUES (..., 1)", ..., transaction);
	}
	// 保存子行；先删除再重新插入是最简单的做法
});
```

### EF Core 注意事项

- 在模型配置中忽略 `DomainEvents`。
- 当只有子行变化时，根上的 `rowversion` 列不会更新。请显式地触碰根，以便检测到冲突。
- `Save` 可以对未被跟踪的聚合执行添加，对已被跟踪的聚合则什么也不做。

## 逻辑锁

有些数据需要在用户处理期间被长时间锁定。锁应当放在哪里，取决于它的用途。

| 用途 | 示例 | 位置 |
|---|---|---|
| 锁具有业务含义 | “审批期间不可编辑” | 聚合的状态，配合 `Lock` 和 `Unlock` 等操作 |
| 锁用于防止 UI 上的同时编辑 | “Alice 正在编辑这条记录” | 应用层中的独立服务 |

无论哪种情况，都应为锁设置过期时间，为管理员提供解除锁定的手段，并保留乐观锁作为最后一道防线。
