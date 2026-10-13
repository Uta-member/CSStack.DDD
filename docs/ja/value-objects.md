# 値オブジェクト

値オブジェクトは、その属性だけによって定義される不変のオブジェクトです。同じ属性を持つ 2 つの値オブジェクトは、同じ値とみなされます。

## インターフェース

| インターフェース | 用途 |
|---|---|
| `IValueObject<TSelf>` | 値オブジェクト。`IEquatable<TSelf>` の実装が必要です。`record` で実装すると `==`・`!=` 演算子も得られます。 |
| `ISingleValueObject<TSelf, TValue>` | メールアドレスや識別子など、単一の値（`Value`）をラップする値オブジェクト。 |

## レコードによる実装

C# の `record` は、コンパイラーがすべてのプロパティに基づいて `Equals`、`GetHashCode`、`==`、`!=` を生成するため、自動的に `IValueObject<TSelf>` を満たします。

```csharp
public sealed record Address(PostalCode PostalCode, string City) : IValueObject<Address>;
```

> [!WARNING]
> レコードは、配列や `List<T>` などのコレクションを内容ではなく**参照で**比較します。値オブジェクトがコレクションを含む場合は、`Equals` と `GetHashCode` をオーバーライドして要素を比較するか、値で比較されるコレクション型を使用してください。

## 単一値オブジェクトと強く型付けされた識別子

`ISingleValueObject<TSelf, TValue>` はプリミティブ型をラップするのに便利です。これにより、メールアドレスと名前、会員 ID と注文 ID をコンパイラーが区別できるようになります。

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

強く型付けされた識別子はエンティティの `TId : notnull, IEquatable<TId>` 制約を満たすため、そのままエンティティの識別子として使用できます。

## コンストラクターで確認すべきこと

**コンストラクターでは、null でないことなど、型が存在するための最低限の条件だけを確認してください。ビジネスルールをコンストラクターで強制してはいけません。**

```csharp
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value)); // 値がなければ型として存在できない
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

理由は永続化にあります。もしコンストラクターが不正な値を拒否すると、次のような問題が起こります。

- ルールが変わった後、古いルールのもとで保存されたデータを読み込めなくなります。
- 不正な値が 1 つあるだけで集約全体を読み込めなくなり、その値を修正するための操作すら実行できなくなります。
- データを修復する手段が、データベースを手作業で編集することしかなくなります。

その代わりに、値オブジェクトは自身のルールに関する**知識**を `Validate()` に持ち、それをいつ強制するかは**エンティティのライフサイクル**が決めます。生成時と変更時には強制し、再構築時には強制しません。[エンティティと集約](entities-and-aggregates.md) と [バリデーション](validation.md) を参照してください。

このトレードオフとして、型だけでは値の妥当性が保証されなくなります。妥当性は、すべての書き込みが入力を検証するドメインの操作を経由することによって保証されます。

## 複数のプロパティを持つ値オブジェクト

値オブジェクトが他の値オブジェクトを含む場合は、`Under` を使って各メンバーのエラーにそのメンバー名を前置します。

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

複数のプロパティにまたがるルールは、値オブジェクト全体に属します。空のパスで報告してください。

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
