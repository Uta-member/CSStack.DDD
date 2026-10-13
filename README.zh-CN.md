# CSStack.DDD

[English](README.md) | [日本語](README.ja.md) | **简体中文**

一个轻量级工具包，为在 C# 中实践领域驱动设计（DDD）提供基础构件。

CSStack.DDD 为值对象、实体、聚合根、领域事件、仓储和工作单元提供了接口与基类。它不依赖任何持久化技术，因此可以与 EF Core、Dapper 或其他任意数据访问库搭配使用。

## 特性

- **值对象**：基于属性判断相等（`IValueObject`、`ISingleValueObject`）
- **实体**：基于标识判断相等，并由基类强制保证（`IEntity`、`Entity`）
- **聚合根**：可记录领域事件（`IAggregateRoot`、`AggregateRoot`）
- **领域事件**：在提交时由基础设施层统一收集，可直接用于发件箱模式（Outbox）（`IDomainEvent`、`IDomainEventSource`）
- **校验**：使用强类型错误，并带有 `Address.PostalCode` 这样的成员路径（`IValidatable`、`ValidationError`）
- **仓储**与**工作单元**：让事务不进入领域层（`IRepository`、`IDeletableRepository`、`IUnitOfWork`）

## 环境要求

- 支持 .NET Standard 2.0 的环境（.NET 8 或更高版本、.NET Framework 4.7.2 或更高版本等）
- 在 .NET 8 或更高版本中支持 Native AOT 和裁剪（trimming）
- 以 record 实现值对象时需要 C# 9 或更高版本（[详情](docs/zh-CN/getting-started.md#安装)）

## 安装

```shell
dotnet add package CSStack.DDD
```

## 快速示例

```csharp
using CSStack.DDD.Application;
using CSStack.DDD.Domain;

// 包装单个值的值对象
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value)); // 只检查该类型得以存在的最低条件
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}

// 聚合根
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	// 创建：校验输入并发布事件
	public static Member Register(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	// 从持久化数据重建：不校验，也不发布事件
	public static Member Reconstruct(MemberId id, Email email) => new(id, email);
}

// 应用层中的用例
public sealed class RegisterMemberUseCase(IRepository<Member, MemberId> members, IUnitOfWork unitOfWork)
{
	public async Task<MemberId> ExecuteAsync(string email, CancellationToken cancellationToken)
	{
		var member = Member.Register(new Email(email), DateTimeOffset.UtcNow);
		members.Save(member);                         // 仅做登记
		await unitOfWork.CommitAsync(cancellationToken); // 在一个事务中写入聚合及其事件
		return member.Id;
	}
}
```

## 文档

| 主题 | 说明 |
|---|---|
| [快速入门](docs/zh-CN/getting-started.md) | 安装方法，以及各个基础构件的逐步讲解 |
| [值对象](docs/zh-CN/value-objects.md) | 相等性、单值对象，以及构造函数中应当检查什么 |
| [实体与聚合](docs/zh-CN/entities-and-aggregates.md) | 标识、生命周期（创建与重建）以及操作 |
| [领域事件](docs/zh-CN/domain-events.md) | 事件的记录、收集与分发 |
| [校验](docs/zh-CN/validation.md) | 强类型错误、路径，以及诊断与强制的区别 |
| [仓储与工作单元](docs/zh-CN/repositories-and-unit-of-work.md) | 持久化、事务与并发控制 |
| [设计决策](docs/zh-CN/design-decisions.md) | 本工具包为何如此设计 |

## 命名空间

| 命名空间 | 层 | 内容 |
|---|---|---|
| `CSStack.DDD.Domain` | 领域层 | 值对象、实体、聚合、领域事件、仓储、校验 |
| `CSStack.DDD.Application` | 应用层 | 工作单元、并发冲突异常 |

## 许可证

本项目基于 [MIT License](LICENSE.txt) 授权。
