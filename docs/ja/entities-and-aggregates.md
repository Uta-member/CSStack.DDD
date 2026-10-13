# エンティティと集約

## エンティティ

エンティティは、属性ではなく同一性によって定義されるオブジェクトです。メールアドレスを変更した会員も、同じ会員であることに変わりはありません。

| 型 | 用途 |
|---|---|
| `IEntity<TSelf, TId>` | エンティティ。`Id`、`IEquatable<TSelf>`、`==`・`!=` 演算子の実装が必要です。 |
| `Entity<TSelf, TId>` | 識別子による等価性を実装した基底クラス。 |

### 識別子による等価性

`Entity<TSelf, TId>` はエンティティを `Id` で比較します。`Equals(object)` と `GetHashCode()` は **sealed** になっているため、派生クラスで同一性の比較方法を変更することはできません。

```csharp
public sealed class Member : Entity<Member, MemberId>
{
	public override MemberId Id { get; }
	// ...
}
```

- 同じ型で同じ `Id` を持つ 2 つのエンティティは、他の属性が異なっていても等しいとみなされます。これが同一性の意味です。
- 型が異なるエンティティは、識別子の基になる値が同じであっても、等しいとみなされることはありません。
- 型のチェックには `GetType()` ではなく `is TSelf` を使用しているため、ORM が生成するプロキシクラスでも正しく動作します。

2 つのエンティティの内容を比較したい場合（テストなど）は、プロパティを明示的に比較するか、オブジェクトを構造的に比較できるアサーションライブラリを使用してください。

### 識別子は生成時に割り当てる

識別子は、エンティティの生成時にドメイン内で生成してください（例：`MemberId.New()`）。

データベースで識別子を生成すると、新しいエンティティはすべて保存されるまで同じ既定値（`0` など）を共有するため、互いに等しいとみなされてしまいます。また、保存後にハッシュコードが変わるため、`HashSet` や `Dictionary` が正しく動作しなくなります。

## 集約ルート

集約とは、1 つの単位として整合性が保たれるエンティティと値オブジェクトのまとまりです。集約ルートは、集約に対するすべての変更の窓口となるエンティティです。

| 型 | 用途 |
|---|---|
| `IAggregateRoot<TSelf, TId>` | 集約ルート。`IDomainEventSource` でもあります。 |
| `AggregateRoot<TSelf, TId>` | ドメインイベントを記録する基底クラス。`Entity<TSelf, TId>` から派生しています。 |

リポジトリは集約ルートごとにのみ定義します。集約内部のエンティティを単独で読み込んだり保存したりすることはありません。

## ライフサイクル：生成と再構築

状態の妥当性は、エンティティのライフサイクルが責任を持ちます。新しいエンティティの生成と、既存エンティティの再構築には、別々のファクトリメソッドを用意してください。

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

	// 生成：入力を検証し、ビジネスポリシーを適用して、イベントを発行する
	public static Member Register(MemberName name, Email email, TimeProvider time)
	{
		List<ValidationError> errors =
		[
			.. name.Validate().Under(nameof(Name)),
			.. email.Validate().Under(nameof(Email)),
		];
		if (name.Value.Length > 20)
		{
			errors.Add(new TooLong(20, name.Value.Length) { Path = nameof(Name) }); // 登録時のポリシー
		}
		errors.ThrowIfInvalid();

		var member = new Member(MemberId.New(), name, email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), time.GetUtcNow(), member.Id, email));
		return member;
	}

	// 再構築：過去の状態を復元するだけなので、検証もイベント発行も行わない
	public static Member Reconstruct(MemberId id, MemberName name, Email email)
	{
		return new Member(id, name, email);
	}
}
```

`Create` や `Reconstruct` はエンティティごとにパラメーターが異なるため、このツールキットではインターフェースとして定義していません。規約として扱ってください。

## 操作

状態の変更は、セッターではなく、業務上の意図に沿った名前の操作を通じて行います。

```csharp
public void ChangeEmail(Email email, TimeProvider time)
{
	email.Validate().Under(nameof(Email)).ThrowIfInvalid(); // ゲート
	Email = email;
	AddDomainEvent(new MemberEmailChanged(Guid.NewGuid(), time.GetUtcNow(), Id, email));
}
```

- **上書きよりも意図を優先してください。** `UpdateMember(name, email, address, ...)` のようにすべてのフィールドを上書きする操作は、他者による変更を気づかないうちに破棄してしまうおそれがあります。`ChangeEmail` のような操作は、変更しようとしているものだけに触れます。
- **入力に関するものではないビジネスルール**（たとえば「退会済みの会員はメールアドレスを変更できない」）も、操作の中で確認します。こうしたルールには独自の例外をスローしてください。

## 操作を行ったのは誰か

「誰が」には 2 種類あり、区別して扱います。

| 種類 | 例 | 置き場所 |
|---|---|---|
| 業務上の意味を持つ「誰が」 | 承認者、依頼者、担当者 | 集約の状態。操作の引数として渡します。 |
| 監査のために記録する「誰が」 | 最終更新者、作成者 | インフラストラクチャ。コミット時に実行コンテキストから記録します。 |

```csharp
public void Approve(UserId approver, TimeProvider time)
{
	if (approver == RequestedBy)
	{
		throw new ApprovalRuleException("The requester cannot approve their own request.");
	}
	ApprovedBy = approver;
	AddDomainEvent(new OrderApproved(Guid.NewGuid(), time.GetUtcNow(), Id, approver));
}
```

`UpdatedBy` や `UpdatedAt` のような監査用の列を集約に追加しないでください。

## 業務上の意味を持つ削除

退会やキャンセルのように「削除」が業務上の意味を持つ場合は、状態遷移としてモデリングし、他の変更と同じように保存します。

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

`IDeletableRepository` は、実際に存在しなくなる集約にのみ使用してください。[リポジトリと Unit of Work](repositories-and-unit-of-work.md) を参照してください。

退会済みの集約を、インフラストラクチャで自動的に除外しないでください（グローバルクエリフィルターなど）。現在の状態で何ができるかは、集約自身が判断すべきです。
