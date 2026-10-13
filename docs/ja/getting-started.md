# はじめに

このガイドでは、シンプルな会員登録をモデリングしながら、CSStack.DDD の各ビルディングブロックを順に紹介します。

## インストール

```shell
dotnet add package CSStack.DDD
```

CSStack.DDD は .NET Standard 2.0、.NET 8、.NET 10 を対象としているため、.NET Framework（4.7.2 以降を推奨）からも利用できます。

このドキュメントの例では record を使用しています。.NET Framework で record を使うには、プロジェクトファイルで `LangVersion` を `9.0` 以降（例えば `latest`）に設定し、プロジェクト内に次の型を定義してください（PolySharp などの polyfill パッケージを使っても構いません）。

```csharp
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
```

## 2 つの名前空間

| 名前空間 | 使用するレイヤー |
|---|---|
| `CSStack.DDD.Domain` | ドメイン層：値オブジェクト、エンティティ、集約、ドメインイベント、リポジトリのインターフェース、バリデーション |
| `CSStack.DDD.Application` | アプリケーション層：Unit of Work、同時実行競合の例外 |

ドメイン層のコードに `using CSStack.DDD.Application;` が含まれている場合は、責務がレイヤーをまたいで漏れ出しているサインです。

## ステップ 1：値オブジェクト

値オブジェクトは、その属性によって定義されます。`record` として実装すれば、属性による等価性が自動的に得られます。

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

コンストラクターでは形式を検証していない点に注目してください。確認しているのは、型が存在するために必要なこと（`null` でないこと）だけです。ルールは `Validate()` が報告し、それをいつ強制するかはエンティティが決めます。その理由については [値オブジェクト](value-objects.md) を参照してください。

## ステップ 2：ドメインイベント

ドメインイベントは、起きた出来事を表す不変の記録です。

```csharp
public sealed record MemberRegistered(Guid EventId, DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : IDomainEvent;
```

## ステップ 3：集約ルート

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
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
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

- `Register` は新しい会員を生成します。入力を検証し、イベントを発行します。
- `Reconstruct` は永続化層から会員を復元します。検証もイベント発行も行わないため、古いルールのもとで保存されたデータでも必ず読み込んで修正できます。
- `ChangeEmail` は操作です。状態を変更する前に入力を検証します。

等価性はすでに処理されています。同じ `Id` を持つ 2 つの `Member` インスタンスは等しいとみなされます。

## ステップ 4：リポジトリ

ドメイン層で、集約ごとにリポジトリのインターフェースを定義します。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

実装はインフラストラクチャ層に置きます。[リポジトリと Unit of Work](repositories-and-unit-of-work.md) を参照してください。

## ステップ 5：ユースケース

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.FindAsync(new MemberId(memberId), cancellationToken)
			?? throw new KeyNotFoundException($"Member {memberId} was not found.");

		member.ChangeEmail(new Email(email));
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

`Save` は集約を登録するだけです。`CommitAsync` が、登録されたすべての集約とそのドメインイベントを 1 つのトランザクションで書き込みます（イベントは Outbox に書き込みます）。イベントへの反応は、コミットの後に Outbox の発行プロセスが別のトランザクションで実行します。

## ステップ 6：例外を処理する

このツールキットは次の例外を使用します。プレゼンテーション層で一度だけ、グローバルに処理してください。

| 例外 | 意味 | 一般的な HTTP ステータス |
|---|---|---|
| `DomainValidationException` | 操作の入力がルールに違反しています。`Errors` にすべての違反が含まれます。 | 400 |
| `ConcurrencyConflictException` | 別のトランザクションが先に集約を変更しました。 | 409 |

ASP.NET Core での例は [バリデーション](validation.md) を参照してください。

## 次のステップ

- [値オブジェクト](value-objects.md)
- [エンティティと集約](entities-and-aggregates.md)
- [ドメインイベント](domain-events.md)
- [バリデーション](validation.md)
- [リポジトリと Unit of Work](repositories-and-unit-of-work.md)
- [設計判断](design-decisions.md)
