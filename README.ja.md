# CSStack.DDD

[English](README.md) | **日本語** | [简体中文](README.zh-CN.md)

C# でドメイン駆動設計（DDD）を実践するための、軽量なビルディングブロック集です。

CSStack.DDD は、値オブジェクト、エンティティ、集約ルート、ドメインイベント、リポジトリ、Unit of Work のためのインターフェースと基底クラスを提供します。特定の永続化技術には依存しないため、EF Core、Dapper をはじめ、どのデータアクセスライブラリとも組み合わせて使えます。

## 特長

- 属性による等価性を持つ**値オブジェクト**（`IValueObject`、`ISingleValueObject`）
- 識別子による等価性を基底クラスで保証する**エンティティ**（`IEntity`、`Entity`）
- ドメインイベントを記録する**集約ルート**（`IAggregateRoot`、`AggregateRoot`）
- コミット時にインフラストラクチャが回収する、Outbox パターンにそのまま対応した**ドメインイベント**（`IDomainEvent`、`IDomainEventSource`）
- 型付きのエラーと `Address.PostalCode` のようなメンバーパスを備えた**バリデーション**（`IValidatable`、`ValidationError`）
- トランザクションをドメインから切り離す**リポジトリ**と **Unit of Work**（`IRepository`、`IDeletableRepository`、`IUnitOfWork`）

## 動作要件

- .NET 10 以降

## インストール

```shell
dotnet add package CSStack.DDD
```

## 簡単な例

```csharp
using CSStack.DDD.Application;
using CSStack.DDD.Domain;

// 単一の値をラップする値オブジェクト
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		ArgumentNullException.ThrowIfNull(value); // 型が存在するための最低限の条件だけを確認する
		Value = value;
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains('@')) return [new InvalidFormat()];
		return [];
	}
}

// 集約ルート
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	// 生成：入力を検証し、イベントを発行する
	public static Member Register(Email email, TimeProvider time)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), time.GetUtcNow(), member.Id, email));
		return member;
	}

	// 永続化層からの再構築：検証もイベント発行も行わない
	public static Member Reconstruct(MemberId id, Email email) => new(id, email);
}

// アプリケーション層のユースケース
public sealed class RegisterMemberUseCase(IRepository<Member, MemberId> members, IUnitOfWork unitOfWork, TimeProvider time)
{
	public async Task<MemberId> ExecuteAsync(string email, CancellationToken cancellationToken)
	{
		var member = Member.Register(new Email(email), time);
		members.Save(member);                         // 登録するだけ
		await unitOfWork.CommitAsync(cancellationToken); // 集約とそのイベントを 1 つのトランザクションで書き込む
		return member.Id;
	}
}
```

## ドキュメント

| トピック | 内容 |
|---|---|
| [はじめに](docs/ja/getting-started.md) | インストールと、各ビルディングブロックの使い方の流れ |
| [値オブジェクト](docs/ja/value-objects.md) | 等価性、単一値オブジェクト、コンストラクターで確認すべきこと |
| [エンティティと集約](docs/ja/entities-and-aggregates.md) | 同一性、ライフサイクル（生成と再構築）、操作 |
| [ドメインイベント](docs/ja/domain-events.md) | イベントの記録・回収・配信 |
| [バリデーション](docs/ja/validation.md) | 型付きエラー、パス、診断と強制の違い |
| [リポジトリと Unit of Work](docs/ja/repositories-and-unit-of-work.md) | 永続化、トランザクション、同時実行制御 |
| [設計判断](docs/ja/design-decisions.md) | このツールキットがなぜこのように設計されているのか |

## 名前空間

| 名前空間 | レイヤー | 内容 |
|---|---|---|
| `CSStack.DDD.Domain` | ドメイン層 | 値オブジェクト、エンティティ、集約、ドメインイベント、リポジトリ、バリデーション |
| `CSStack.DDD.Application` | アプリケーション層 | Unit of Work、同時実行競合の例外 |
