# 实体与聚合

## 实体

实体是由标识而非属性来定义的对象。会员即使更改了电子邮件地址，也仍然是同一个会员。

| 类型 | 用途 |
|---|---|
| `IEntity<TSelf, TId>` | 实体。要求实现 `Id` 和 `IEquatable<TSelf>`。继承 `Entity<TSelf, TId>` 时还会获得 `==` 和 `!=` 运算符。 |
| `Entity<TSelf, TId>` | 实现了基于标识符相等性的基类。 |

### 基于标识符的相等性

`Entity<TSelf, TId>` 按 `Id` 比较实体。`Equals(object)` 和 `GetHashCode()` 是 **sealed** 的，因此派生类无法改变标识的比较方式。

```csharp
public sealed class Member : Entity<Member, MemberId>
{
	public override MemberId Id { get; }
	// ...
}
```

- 同一类型、`Id` 相同的两个实体是相等的，即使其他属性不同。这正是“标识”的含义。
- 不同类型的实体永远不相等，即使它们的标识符底层值相同。
- 类型检查使用的是 `is TSelf` 而不是 `GetType()`，因此对 ORM 生成的代理类同样有效。

如果需要比较两个实体的内容（例如在测试中），请显式比较它们的属性，或使用能够按结构比较对象的断言库。

### 在创建时分配标识符

在创建实体时由领域生成标识符，例如使用 `MemberId.New()`。

如果由数据库生成标识符，那么在保存之前，所有新实体都持有相同的默认值（如 `0`），从而被视为相等。此外，保存之后哈希码也会改变，这会破坏 `HashSet` 和 `Dictionary` 的行为。

## 聚合根

聚合是一组作为整体保持一致的实体和值对象。聚合根是对聚合进行所有变更时必须经由的那个实体。

| 类型 | 用途 |
|---|---|
| `IAggregateRoot<TSelf, TId>` | 聚合根。同时也是 `IDomainEventSource`。必须是类。 |
| `AggregateRoot<TSelf, TId>` | 可记录领域事件的基类。派生自 `Entity<TSelf, TId>`。 |

仓储只为聚合根定义。聚合内部的实体不会被单独加载或保存。

## 生命周期：创建与重建

状态是否有效，由实体的生命周期负责。请分别为创建新实体和重建已有实体提供不同的工厂方法。

```csharp
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public MemberName Name { get; private set; }
	public Email Email { get; private set; }

	private Member(MemberId id, MemberName name, Email email)
	{
		Id = id;
		Name = name;
		Email = email;
	}

	// 创建：校验输入、应用业务策略并发布事件
	public static Member Register(MemberName name, Email email, DateTimeOffset occurredAt)
	{
		List<ValidationError> errors =
		[
			.. name.Validate().Under(nameof(Name)),
			.. email.Validate().Under(nameof(Email)),
		];
		if (name.Value.Length > 20)
		{
			errors.Add(new TooLong(20, name.Value.Length) { Path = nameof(Name) }); // 注册时适用的策略
		}
		errors.ThrowIfInvalid();

		var member = new Member(MemberId.New(), name, email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	// 重建：恢复过去的状态，因此不校验，也不发布事件
	public static Member Reconstruct(MemberId id, MemberName name, Email email)
	{
		return new Member(id, name, email);
	}
}
```

本工具包没有在接口中定义 `Create` 或 `Reconstruct`，因为每个实体的参数都不相同。请将它们作为约定来遵循。

## 操作

状态的变更通过以业务意图命名的操作来完成，而不是通过 setter。

```csharp
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
{
	email.Validate().Under(nameof(Email)).ThrowIfInvalid(); // 关卡
	Email = email;
	AddDomainEvent(new MemberEmailChanged(Guid.NewGuid(), occurredAt, Id, email));
}
```

- **用意图表达变更，而不是整体覆盖。** 像 `UpdateMember(name, email, address, ...)` 这样覆盖所有字段的操作，可能会悄无声息地丢弃他人所做的修改。而 `ChangeEmail` 这样的操作只会改动它本来要改的部分。
- **与输入无关的业务规则**（例如“已退会的会员不能更改电子邮件地址”）同样在操作中检查。对于这类规则，请抛出派生自 `DomainException` 的自定义异常。

```csharp
public sealed class MembershipRuleException : DomainException
{
	public MembershipRuleException(string message) : base(message)
	{
	}
}
```

`DomainValidationException` 同样派生自 `DomainException`，因此表示层可以在一处统一处理所有被领域拒绝的操作。请参阅[校验](validation.md#aspnet-core-示例)。

## 操作的执行者

“谁”有两种，需要加以区分：

| 种类 | 示例 | 归属位置 |
|---|---|---|
| 具有业务含义的“谁” | 审批人、申请人、负责人 | 聚合的状态。作为操作的参数传入。 |
| 用于审计记录的“谁” | 最后修改人、创建人 | 基础设施层。在提交时根据执行上下文记录。 |

```csharp
public void Approve(UserId approver, DateTimeOffset occurredAt)
{
	if (approver == RequestedBy)
	{
		throw new ApprovalRuleException("The requester cannot approve their own request.");
	}
	ApprovedBy = approver;
	AddDomainEvent(new OrderApproved(Guid.NewGuid(), occurredAt, Id, approver));
}
```

不要在聚合中添加 `UpdatedBy` 或 `UpdatedAt` 之类的审计列。

## 具有业务含义的删除

如果“删除”在业务上具有含义，例如退会或取消，请将其建模为状态迁移，并像其他变更一样保存。

```csharp
public void Withdraw()
{
	if (IsWithdrawn)
	{
		throw new MembershipRuleException("The member has already withdrawn.");
	}
	IsWithdrawn = true;
}
```

只有对真正会消失的聚合才使用 `IDeletableRepository`。请参阅[仓储与工作单元](repositories-and-unit-of-work.md)。

不要在基础设施层中自动过滤掉已退会的聚合（例如使用全局查询过滤器）。在当前状态下能做什么，应当由聚合自身来判断。
