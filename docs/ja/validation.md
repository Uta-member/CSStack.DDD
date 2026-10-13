# バリデーション

## 診断と強制

CSStack.DDD では、バリデーションを 2 つの役割に分けています。

| | 診断 | ゲート |
|---|---|---|
| 担当 | `IValidatable.Validate()` | エンティティの操作 |
| 役割 | すべての問題を報告する | 不正な変更を拒否する |
| 方法 | エラーを**返す** | `DomainValidationException` を**スローする** |
| 理由 | すべてのエラーを収集して結合できる。例外では最初のエラーで止まってしまう。 | 呼び出し側が無視できない。 |

`Validate()` は報告するだけです。強制はエンティティの操作という 1 か所でのみ行われるため、その操作をテストすることで検証できます。

## バリデーションエラー

エラーの種類は型で表し、エラーを説明する値はその型のプロパティとして持ちます。解析が必要な文字列コードや辞書はありません。

| 型 | 意味 |
|---|---|
| `Required` | 値が必須だが指定されていない。 |
| `InvalidFormat` | 値が期待される形式ではない。 |
| `TooLong(MaxLength, ActualLength)` | 値が長すぎる。 |
| `TooShort(MinLength, ActualLength)` | 値が短すぎる。 |
| `OutOfRange<T>(Min, Max, Actual)` | 値が許容範囲外である。 |

ドメイン固有のエラーは、`ValidationError` から派生させて定義します。

```csharp
public sealed record UnderAge(int MinimumAge, int ActualAge) : ValidationError;
```

エラーはレコードなので、値で比較されます。そのため、テストでのアサーションが簡単です：`Assert.Equal(new TooLong(20, 25) { Path = "Name" }, error)`。

## パス

`ValidationError.Path` は、`Address.PostalCode` や `Lines[2].Quantity` のように、エラーが発生した場所を示します。空のパスはオブジェクト自身を意味します。

各階層は、`Under` を使って自分自身のメンバー名だけを追加します。値オブジェクトは、自分がどこで使われているかを知りません。

```
OrderLine.Quantity.Validate()    → TooLong { Path = "" }
  ↓ OrderLine が .Under("Quantity") を追加
OrderLine.Validate()             → TooLong { Path = "Quantity" }
  ↓ Order が .Under("Lines[2]") を追加
Order.Validate()                 → TooLong { Path = "Lines[2].Quantity" }
```

```csharp
public IReadOnlyList<ValidationError> Validate() =>
[
	.. ShippingAddress.Validate().Under(nameof(ShippingAddress)),
	.. DeliveryPeriod.Validate().Under(nameof(DeliveryPeriod)),
	.. Lines.SelectMany((line, i) => line.Validate().Under($"{nameof(Lines)}[{i}]")),
];
```

複数のプロパティにまたがるルール（たとえば「開始日は終了日より前でなければならない」）は、オブジェクト全体のエラーとして、空のパスで報告します。

## ゲート

エンティティの操作では `ThrowIfInvalid` を使用します。

```csharp
// 単一の入力
public void ChangeShippingAddress(Address address)
{
	address.Validate().Under(nameof(ShippingAddress)).ThrowIfInvalid();
	ShippingAddress = address;
}

// 複数の入力とビジネスポリシー
public static Order Place(OrderId id, Address address, Period period, IReadOnlyList<OrderLine> lines)
{
	List<ValidationError> errors =
	[
		.. address.Validate().Under(nameof(ShippingAddress)),
		.. period.Validate().Under(nameof(DeliveryPeriod)),
		.. lines.SelectMany((line, i) => line.Validate().Under($"{nameof(Lines)}[{i}]")),
	];
	if (lines.Count == 0)
	{
		errors.Add(new Required { Path = nameof(Lines) });
	}
	errors.ThrowIfInvalid();

	return new Order(id, address, period, lines);
}
```

## 2 種類のルール

| | 値のルール | 操作のポリシー |
|---|---|---|
| 例 | メールアドレスに `@` が含まれる、金額が負でない | 登録時の名前は 20 文字以内、会員は 18 歳以上でなければならない |
| 時間とともに変わるか | めったに変わらない | 業務上の判断によって変わる |
| 置き場所 | 値オブジェクトの `Validate()` | エンティティの操作 |

どちらも操作によって強制され、再構築時には強制されません。そのため、古いルールのもとで保存されたデータでも必ず読み込めます。

「退会済みの会員はメールアドレスを変更できない」のような入力に関するものではないルールは、指し示す入力がないので、バリデーションエラーとしては報告しません。こうしたルールには、`DomainException` から派生した独自の例外をスローしてください。[操作](entities-and-aggregates.md#操作)を参照してください。

## エラーをメッセージに変換する

エラーはメッセージを含みません。メッセージは、プレゼンテーション層で一度だけ組み立てます。

```csharp
public sealed class ValidationMessageFormatter
{
	public string Format(ValidationError error) => error switch
	{
		Required => "This field is required.",
		InvalidFormat => "The format is invalid.",
		TooLong e => $"Must be {e.MaxLength} characters or fewer.",
		TooShort e => $"Must be {e.MinLength} characters or more.",
		_ => "The value is invalid.",
	};
}
```

クラス階層に対する `switch` がすべての型を網羅しているかどうかはコンパイラーがチェックしないため、`_` のアームは残しておいてください。

## ASP.NET Core での例

`DomainException` は一度だけ、グローバルに処理します。そうすれば、コントローラーやユースケースにエラー処理のコードは不要になります。`DomainValidationException` も `DomainException` なので、先に判定します。

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

パスは ASP.NET Core の `ModelState` のキーと同じ形式なので、クライアント側でそれぞれの入力フィールドに対応付けられます。

それ以外の `DomainException` は、入力に関するものではないビジネスルールによる拒否なので、パスを持ちません。そのメッセージを利用者に表示し、ローカライズが必要な場合は、バリデーションエラーと同じように、例外の型をメッセージに変換してください。

## フォームの名前で入力を検証する

フォームやリクエスト DTO の名前は、ドメインの名前と異なる場合があります（たとえば、フォームでは `ZipCode`、ドメインでは `Address.PostalCode`）。値オブジェクトはコンストラクターで検証を行わないため、各フィールドを直接検証し、フォームで使われている名前を付加できます。

```csharp
List<ValidationError> errors =
[
	.. new Email(request.Email).Validate().Under(nameof(request.Email)),
	.. new PostalCode(request.ZipCode).Validate().Under(nameof(request.ZipCode)),
];
errors.ThrowIfInvalid();
```

ルールは引き続き値オブジェクトの中で一度だけ定義されています。ドメインのゲートは、最後の防御線として残ります。
