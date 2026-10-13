# 值对象

值对象是只由其属性定义的不可变对象。属性相同的两个值对象就是同一个值。

## 接口

| 接口 | 用途 |
|---|---|
| `IValueObject<TSelf>` | 值对象。要求实现 `IEquatable<TSelf>`。以 `record` 实现时还会获得 `==` 和 `!=` 运算符。 |
| `ISingleValueObject<TSelf, TValue>` | 包装单个值（`Value`）的值对象，例如电子邮件地址或标识符。 |

## 用 record 实现

C# 的 `record` 会自动满足 `IValueObject<TSelf>`，因为编译器会基于所有属性生成 `Equals`、`GetHashCode`、`==` 和 `!=`。

```csharp
public sealed record Address(PostalCode PostalCode, string City) : IValueObject<Address>;
```

> [!WARNING]
> record 对数组以及 `List<T>` 等集合是**按引用**比较的，而不是按内容比较。如果值对象包含集合，请重写 `Equals` 和 `GetHashCode` 以逐个比较元素，或者使用按值比较的集合类型。

## 单值对象与强类型标识符

`ISingleValueObject<TSelf, TValue>` 适合用来包装基元类型，让编译器能够区分电子邮件地址和姓名，或者区分会员 ID 和订单 ID。

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
```

强类型标识符满足实体的 `TId : notnull, IEquatable<TId>` 约束，因此可以直接用作实体的标识符。

## 构造函数中应当检查什么

**只检查该类型得以存在的最低条件，例如非 null。不要在构造函数中强制执行业务规则。**

```csharp
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value)); // 没有值，该类型就无法存在
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

原因在于持久化。如果构造函数拒绝无效值：

- 规则变更后，按旧规则保存的数据将无法再加载。
- 只要有一个无效值，整个聚合就无法加载，连用来修正该值的操作也无法执行。
- 修复数据的唯一办法就只剩手动修改数据库。

因此，值对象在 `Validate()` 中掌握规则的**知识**，而何时强制执行这些规则则由**实体的生命周期**决定：在创建和变更时执行，在重建时不执行。请参阅[实体与聚合](entities-and-aggregates.md)和[校验](validation.md)。

代价是，仅靠类型本身已无法保证有效性。有效性的保证来自于：所有写入都必须经过领域中的某个操作，而该操作会校验其输入。

## 包含多个属性的值对象

当值对象包含其他值对象时，使用 `Under` 为每个成员的错误加上该成员名作为前缀。

```csharp
public sealed record Address(PostalCode PostalCode, Prefecture Prefecture) : IValueObject<Address>, IValidatable
{
	public IReadOnlyList<ValidationError> Validate() =>
	[
		.. PostalCode.Validate().Under(nameof(PostalCode)),
		.. Prefecture.Validate().Under(nameof(Prefecture)),
	];
}
```

跨越多个属性的规则属于整个值对象。请以空路径报告此类错误。

```csharp
public sealed record InvalidPeriod(DateTime Start, DateTime End) : ValidationError;

public sealed record Period(DateTime Start, DateTime End) : IValueObject<Period>, IValidatable
{
	public IReadOnlyList<ValidationError> Validate()
	{
		return Start > End ? [new InvalidPeriod(Start, End)] : [];
	}
}
```
