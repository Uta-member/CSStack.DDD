# 校验

## 诊断与强制

CSStack.DDD 将校验分为两种角色。

| | 诊断 | 关卡 |
|---|---|---|
| 由谁负责 | `IValidatable.Validate()` | 实体的操作 |
| 做什么 | 报告所有问题 | 拒绝无效的变更 |
| 怎么做 | **返回**错误 | **抛出** `DomainValidationException` |
| 为什么 | 可以收集并合并所有错误。若使用异常，遇到第一个错误就会中止。 | 调用方无法忽略。 |

`Validate()` 只负责报告。强制执行集中在一处，即实体的操作中，因此可以通过测试这些操作来验证。

## 校验错误

错误的种类由其类型表示，描述错误的值则是该类型的属性。不存在需要解析的字符串代码或字典。

| 类型 | 含义 |
|---|---|
| `Required` | 需要值但缺失。 |
| `InvalidFormat` | 值不符合预期格式。 |
| `TooLong(MaxLength, ActualLength)` | 字符串或集合过长。 |
| `TooShort(MinLength, ActualLength)` | 字符串或集合过短。 |
| `BelowMinimum(Minimum, Actual)` | 数值小于最小值。 |
| `AboveMaximum(Maximum, Actual)` | 数值大于最大值。 |

`TooLong` 和 `TooShort` 针对长度，`BelowMinimum` 和 `AboveMaximum` 针对值本身。边界包含边界值，值的类型是 `decimal`，因此无需知道类型参数即可进行模式匹配。范围按两个边界分别检查。对于日期等其他种类的值，请定义自己的错误。

```csharp
if (Value < 1) errors.Add(new BelowMinimum(1, Value));
if (Value > 99) errors.Add(new AboveMaximum(99, Value));
```

通过派生 `ValidationError` 来定义领域特有的错误。

```csharp
public sealed record UnderAge(int MinimumAge, int ActualAge) : ValidationError;
```

错误是 record，因此按值比较。这让它们在测试中很容易断言：`Assert.Equal(new TooLong(20, 25) { Path = "Name" }, error)`。

## 路径

`ValidationError.Path` 表示错误发生的位置，例如 `Address.PostalCode` 或 `Lines[2].Quantity`。空路径表示对象本身。

每一层只用 `Under` 加上自己的成员名。值对象并不知道自己在哪里被使用。

```
OrderLine.Quantity.Validate()    → TooLong { Path = "" }
  ↓ OrderLine 添加 .Under("Quantity")
OrderLine.Validate()             → TooLong { Path = "Quantity" }
  ↓ Order 添加 .Under("Lines", 2)
Order.Validate()                 → TooLong { Path = "Lines[2].Quantity" }
```

```csharp
public IReadOnlyList<ValidationError> Validate() =>
[
	.. ShippingAddress.Validate().Under(nameof(ShippingAddress)),
	.. DeliveryPeriod.Validate().Under(nameof(DeliveryPeriod)),
	.. Lines.SelectMany((line, i) => line.Validate().Under(nameof(Lines), i)),
];
```

对于集合元素，使用 `Under(name, index)` 构建 `Lines[2].Quantity` 这样的路径。索引从 0 开始；传入负值会抛出 `ArgumentOutOfRangeException`。原始错误及其类型和值会被保留，只有在枚举结果时才会枚举错误。如上例所示，将其与带索引的 `SelectMany` 重载结合，即可按顺序校验每个元素，因此无需单独的 `ValidateEach` 辅助方法。

跨越多个属性的规则（例如“开始日期必须早于结束日期”）以空路径报告，作为整个对象的错误。

## 关卡

在实体的操作中使用 `ThrowIfInvalid`。

```csharp
// 单个输入
public void ChangeShippingAddress(Address address)
{
	address.Validate().Under(nameof(ShippingAddress)).ThrowIfInvalid();
	ShippingAddress = address;
}

// 多个输入及业务策略
public static Order Place(OrderId id, Address address, Period period, IReadOnlyList<OrderLine> lines)
{
	List<ValidationError> errors =
	[
		.. address.Validate().Under(nameof(ShippingAddress)),
		.. period.Validate().Under(nameof(DeliveryPeriod)),
		.. lines.SelectMany((line, i) => line.Validate().Under(nameof(Lines), i)),
	];
	if (lines.Count == 0)
	{
		errors.Add(new Required { Path = nameof(Lines) });
	}
	errors.ThrowIfInvalid();

	return new Order(id, address, period, lines);
}
```

## 用于日志的异常消息

`DomainValidationException.Message` 按输入顺序列出前 10 个错误的路径和类型名，例如 `One or more validation errors occurred: Name (TooLong), Email (InvalidFormat)`。路径为空的错误只显示类型名，例如 `Required`。消息不包含错误属性的值。

如果错误超过 10 个，消息末尾会显示省略的数量，例如 `, ... (3 more)`。`Errors` 仍包含所有错误。如果用空序列调用构造函数，消息仍为 `One or more validation errors occurred.`。

此消息用于日志诊断。面向用户的消息应在表示层中根据 `Errors` 组装，如下文所示。

## 两类规则

| | 值的规则 | 操作的策略 |
|---|---|---|
| 示例 | 电子邮件地址包含 `@`；金额不为负数 | 注册时姓名不超过 20 个字符；会员必须年满 18 岁 |
| 随时间变化 | 很少变化 | 由业务决定何时变化 |
| 位置 | 值对象的 `Validate()` | 实体的操作 |

两者都由操作来强制执行，而不在重建时执行，因此按旧规则保存的数据始终可以加载。

像“已退会的会员不能更改电子邮件地址”这样与输入无关的规则，没有可指向的输入，因此不作为校验错误报告。对于这类规则，请抛出派生自 `DomainException` 的自定义异常。请参阅[操作](entities-and-aggregates.md#操作)。

## 将错误转换为消息

错误本身不包含消息。请在表示层中统一组装消息。

```csharp
public sealed class ValidationMessageFormatter
{
	public string Format(ValidationError error) => error switch
	{
		Required => "This field is required.",
		InvalidFormat => "The format is invalid.",
		TooLong e => $"Must be {e.MaxLength} characters or fewer.",
		TooShort e => $"Must be {e.MinLength} characters or more.",
		BelowMinimum e => $"Must be {e.Minimum} or more.",
		AboveMaximum e => $"Must be {e.Maximum} or less.",
		_ => "The value is invalid.",
	};
}
```

对于基于类层次结构的 `switch`，编译器不会检查是否覆盖了所有类型，因此请保留 `_` 分支。

## ASP.NET Core 示例

在全局统一处理一次 `DomainException`。这样控制器和用例中就不再需要错误处理代码。`DomainValidationException` 也是 `DomainException`，因此要先判断它。

```csharp
public sealed class DomainExceptionHandler(ValidationMessageFormatter formatter) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
	{
		if (exception is DomainValidationException validation)
		{
			var errors = validation.Errors
				.GroupBy(e => e.Path)
				.ToDictionary(g => g.Key, g => g.Select(formatter.Format).ToArray());

			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(errors), cancellationToken);
			return true;
		}

		if (exception is DomainException rejected)
		{
			context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
			await context.Response.WriteAsJsonAsync(new ProblemDetails { Detail = rejected.Message }, cancellationToken);
			return true;
		}

		return false;
	}
}
```

路径的形式与 ASP.NET Core `ModelState` 的键相同，因此客户端可以将其映射到对应的输入字段。

其他的 `DomainException` 表示被与输入无关的业务规则拒绝，因此不带路径。如果要向用户显示其消息并需要本地化，请像处理校验错误一样，将异常的类型转换为消息。

## 使用表单中的名称校验输入

表单或请求 DTO 中的名称可能与领域中的名称不同（例如表单中是 `ZipCode`，领域中是 `Address.PostalCode`）。由于值对象不在构造函数中校验，你可以直接校验每个字段，并加上表单所用的名称。

```csharp
List<ValidationError> errors =
[
	.. new Email(request.Email).Validate().Under(nameof(request.Email)),
	.. new PostalCode(request.ZipCode).Validate().Under(nameof(request.ZipCode)),
];
errors.ThrowIfInvalid();
```

规则依然只在值对象中定义一次。领域中的关卡仍作为最后一道防线保留。
