# 快速入门

本指南以一个简单的会员注册场景为例，逐步介绍 CSStack.DDD 的各个基础构件。

## 安装

```shell
dotnet add package CSStack.DDD
```

CSStack.DDD 面向 .NET Standard 2.0、.NET 8 和 .NET 10，因此也可以在 .NET Framework（推荐 4.7.2 或更高版本）中使用。由于不使用反射或运行时代码生成，在 .NET 8 及更高版本中也支持 Native AOT 和裁剪（trimming）。

本文档中的示例使用了集合表达式和主构造函数等 C# 12 功能。请将 `LangVersion` 设置为 `12.0` 或更高版本，并使用支持该版本的编译器。record 本身只需要 C# 9 或更高版本。在 .NET Framework 中，还需要在项目中定义以下类型（也可以使用 PolySharp 等 polyfill 包）。

```csharp
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
```

## 两个命名空间

| 命名空间 | 使用位置 |
|---|---|
| `CSStack.DDD.Domain` | 领域层：值对象、实体、聚合、领域事件、仓储接口和校验 |
| `CSStack.DDD.Application` | 应用层：工作单元与并发冲突异常 |

如果领域层代码中出现了 `using CSStack.DDD.Application;`，说明有职责越过了层的边界。

## 第 1 步：值对象

值对象由其属性来定义。用 `record` 实现值对象，就能直接获得基于属性的相等性。

```csharp
public sealed record MemberId : ISingleValueObject<MemberId, Guid>
{
	public Guid Value { get; }

	public MemberId(Guid value)
	{
		Value = value;
	}

	public static MemberId New() => new(Guid.NewGuid());
}

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

注意，构造函数并不校验格式，只检查该类型得以存在所必需的条件（非 `null`）。规则由 `Validate()` 报告，何时强制执行则由实体决定。具体原因请参阅[值对象](value-objects.md)。

## 第 2 步：领域事件

领域事件是对已发生事情的不可变记录。

```csharp
public sealed record MemberRegistered(DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : DomainEvent(OccurredAt);
```

## 第 3 步：聚合根

```csharp
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
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(occurredAt, member.Id, email));
		return member;
	}

	public static Member Reconstruct(MemberId id, Email email) => new(id, email);

	public void ChangeEmail(Email email)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		Email = email;
	}
}
```

- `Register` 创建新会员。它会校验输入并发布事件。
- `Reconstruct` 从持久化数据中恢复会员。它既不校验也不发布事件，因此按旧规则保存的数据始终可以加载并修正。
- `ChangeEmail` 是一个操作。它在改变状态之前先校验输入。

相等性已经处理好了：`Id` 相同的两个 `Member` 实例是相等的。

## 第 4 步：仓储

在领域层中为每个聚合定义一个仓储接口。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

实现则属于基础设施层。请参阅[仓储与工作单元](repositories-and-unit-of-work.md)。

## 第 5 步：用例

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.GetAsync(new MemberId(memberId), cancellationToken);

		member.ChangeEmail(new Email(email));
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

`GetAsync` 是 `CSStack.DDD.Application` 中的扩展方法。会员不存在时，它会抛出 `AggregateNotFoundException`。如果聚合不存在属于正常结果，请改用 `FindAsync`。

`Save` 只负责登记聚合。`CommitAsync` 会在一个事务中写入所有已登记的聚合及其领域事件（事件写入发件箱）。提交之后，由发件箱发布进程在另一个事务中对事件作出响应。

## 第 6 步：处理异常

本工具包会抛出以下异常。请在表示层中统一进行一次全局处理。

| 异常 | 含义 | 常用 HTTP 状态码 |
|---|---|---|
| `DomainValidationException` | 操作的输入违反了规则。`Errors` 中包含所有违规项。 | 400 |
| 派生自 `DomainException` 的其他类型 | 与输入无关的业务规则拒绝了操作。在领域中定义。 | 422 |
| `AggregateNotFoundException` | 用例所请求的聚合不存在。由 `GetAsync` 抛出。 | 404 |
| `ConcurrencyConflictException` | 另一个事务抢先修改了该聚合。 | 409 |

ASP.NET Core 的示例请参阅[校验](validation.md)。

## 后续阅读

- [值对象](value-objects.md)
- [实体与聚合](entities-and-aggregates.md)
- [领域事件](domain-events.md)
- [校验](validation.md)
- [仓储与工作单元](repositories-and-unit-of-work.md)
- [设计决策](design-decisions.md)
