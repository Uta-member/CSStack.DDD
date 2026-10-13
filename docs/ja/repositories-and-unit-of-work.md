# リポジトリと Unit of Work

## 概要

| 型 | レイヤー | 役割 |
|---|---|---|
| `IRepository<TAggregate, TId>` | ドメイン層 | 集約を検索し、保存対象として登録します。 |
| `IDeletableRepository<TAggregate, TId>` | ドメイン層 | 加えて、集約を削除対象として登録します。 |
| `IUnitOfWork` | アプリケーション層 | 登録されたすべての変更を 1 つのトランザクションでコミットします。 |
| `ConcurrencyConflictException` | アプリケーション層 | 別のトランザクションが先に集約を変更したことを通知します。 |

ドメインはトランザクションについて知りません。トランザクションはユースケースの境界であり、アプリケーション層の関心事です。

## リポジトリ

```csharp
public interface IRepository<TAggregate, TId>
{
	Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken = default);
	void Save(TAggregate aggregate);
}

public interface IDeletableRepository<TAggregate, TId> : IRepository<TAggregate, TId>
{
	void Delete(TAggregate aggregate);
}
```

- `FindAsync` は、.NET の `Find` の慣例に従い、集約が存在しない場合は `null` を返します。
- `Save` と `Delete` は集約を**登録するだけ**です。`IUnitOfWork.CommitAsync` が呼ばれるまで何も書き込まれません。そのため、これらは同期メソッドになっています。
- `Save` は挿入と更新を区別しません。どちらにするかは実装が決めます（後述）。

集約ごとに 1 つのリポジトリインターフェースを定義し、ドメインが必要とするクエリを追加してください。`IQueryable` は公開しないでください。クエリのロジックがリポジトリの外に漏れ出してしまいます。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

### 保存か削除か

`IDeletableRepository` は、実際に存在しなくなる集約に対してのみ実装してください。集約が `Delete` を持つかどうかはドメインの判断であり、それがインターフェースに表れます。

```csharp
public interface IMemberRepository : IRepository<Member, MemberId> { }   // 退会は状態遷移
public interface ICartRepository : IDeletableRepository<Cart, CartId> { } // カートは本当に消える
```

監査のために削除済みの行を残したい場合は、インフラストラクチャで `Delete` を論理削除として実装してください。ドメインの観点からは、集約は存在しなくなっています。

## Unit of Work

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork, TimeProvider time)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.FindAsync(new MemberId(memberId), cancellationToken)
			?? throw new KeyNotFoundException($"Member {memberId} was not found.");

		member.ChangeEmail(new Email(email), time);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- リポジトリと Unit of Work は、DI スコープ（リクエストごとに 1 つ）内で同じ接続を共有します。
- ロールバック用のメソッドはありません。Unit of Work がコミットされずに破棄されると、変更は破棄されます。
- ドメインサービスはコミットしません。受け取った集約を操作するだけで、読み込み・保存・コミットはアプリケーションサービスが行います。

## インフラストラクチャの実装

### 挿入か更新か

実装は、どの集約が読み込まれたかを記憶しておく（アイデンティティマップ）ことで、挿入するか更新するかを判断します。

```
FindAsync(id)   → 集約を「読み込み済み」として記憶する
Save(aggregate) → 読み込み済みなら UPDATE、そうでなければ INSERT
```

挿入しようとした集約がすでに存在する場合は、主キー制約によって拒否されます。`MERGE` や `ON CONFLICT DO UPDATE` のような**データベースの upsert 文は使用しないでください**。既存のデータを気づかないうちに上書きしてしまいます。

### 最初のアクセスでトランザクションを開始する

読み取りと書き込みを同じトランザクションで行うために、トランザクションは `CommitAsync` ではなく、データベースへの最初のアクセス時（通常は `FindAsync`）に開始してください。

### 同時実行制御

ユースケースをトランザクションで囲むだけでは、同時変更を防ぐのに十分では**ありません**。既定の分離レベル（READ COMMITTED）では、2 つのトランザクションが同じ状態を読み取り、どちらも集約のチェックを通過し、どちらも書き込むことができてしまいます。たとえば、2 つのトランザクションがそれぞれ注文に明細を 1 行ずつ追加すると、両方の挿入が成功し、注文の合計が上限を超える可能性があります。

一般的な対処法は、**集約ルートに対する楽観ロック**です。

```
Save(order)
  UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @TokenWhenLoaded
    → 1 行:  子行の処理に進む
    → 0 行: 別のトランザクションが先に更新した → ConcurrencyConflictException をスロー
```

- 子行だけが変更された場合でも、**必ずルートの行を更新してください**。これにより、集約に対するすべての変更が同じ行で衝突するようになります。
- トークンはインフラストラクチャで保持します（アイデンティティマップ内、または EF Core のシャドウプロパティとして）。ドメインに `Version` プロパティは必要ありません。
- 競合が頻繁に起きる集約（在庫、座席予約など）では、代わりに `FindAsync` で悲観ロック（`SELECT ... FOR UPDATE`、SQL Server では `WITH (UPDLOCK)`）を使うことを検討してください。

`ConcurrencyConflictException` がスローされた場合：

- ユーザーによる操作であれば、HTTP 409 を返し、再読み込みを促します。
- 繰り返しても安全な操作（バッチ、イベントハンドラー）であれば、試行回数に上限を設けたうえで、読み込みからユースケース全体を再試行します。

### 集約をまたぐルール

「メールアドレスは会員間で一意である」のような集約をまたぐルールは、個々の集約の同時実行トークンでは保護されません。最後の防御線として、必ずデータベースに一意制約を追加してください。ドメインでのチェックは、わかりやすいエラーを返すためだけのものです。

### ドメインイベントの回収

`CommitAsync` では、登録された各集約に対して `PullDomainEvents()` を呼び出し、集約と同じトランザクションでイベントを Outbox に書き込みます。イベントへの反応はここでは実行しません。Outbox の発行プロセスが、元の変更とは別のトランザクションで実行します。業務が即時の整合性を必要とする場合に限り、コミットの前に反応させることもできます。[ドメインイベント](domain-events.md#配信のタイミング) を参照してください。

### 例：Dapper

次のスケッチは、実装に必要な部分を示したものです。完全な実装ではありません。

```csharp
internal sealed class DapperUnitOfWork(DbConnection connection) : IUnitOfWork, IAsyncDisposable
{
	private DbTransaction? _transaction;
	private readonly Dictionary<object, long> _loaded = new(ReferenceEqualityComparer.Instance); // 集約 → トークン
	private readonly List<Func<DbTransaction, CancellationToken, Task>> _pending = [];

	internal async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken)
	{
		if (_transaction is not null) return _transaction;
		await connection.OpenAsync(cancellationToken);
		return _transaction = await connection.BeginTransactionAsync(cancellationToken);
	}

	internal void MarkLoaded(object aggregate, long token) => _loaded[aggregate] = token;
	internal bool TryGetToken(object aggregate, out long token) => _loaded.TryGetValue(aggregate, out token);
	internal void Enqueue(Func<DbTransaction, CancellationToken, Task> work) => _pending.Add(work);

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		var transaction = await GetTransactionAsync(cancellationToken);
		foreach (var work in _pending) await work(transaction, cancellationToken);
		// ここで、登録された集約からドメインイベントを取り出し、Outbox に書き込む（ドメインイベントを参照）
		await transaction.CommitAsync(cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is not null) await _transaction.DisposeAsync(); // コミットされていなければロールバックされる
		await connection.DisposeAsync();
	}
}
```

```csharp
public void Save(Order order) => _unitOfWork.Enqueue(async (transaction, cancellationToken) =>
{
	var connection = transaction.Connection!;
	if (_unitOfWork.TryGetToken(order, out var token))
	{
		var affected = await connection.ExecuteAsync(
			"UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @Token",
			new { Id = order.Id.Value, Token = token }, transaction);
		if (affected == 0)
		{
			throw new ConcurrencyConflictException(typeof(Order), order.Id);
		}
	}
	else
	{
		await connection.ExecuteAsync("INSERT INTO Orders (..., Token) VALUES (..., 1)", ..., transaction);
	}
	// 子行を保存する。削除してから再挿入するのが最も簡単な方法
});
```

### EF Core を使う場合の注意点

- モデル構成で `DomainEvents` を無視してください。
- ルートの `rowversion` 列は、子行だけが変更された場合には更新されません。競合が検出されるよう、ルートを明示的に更新してください。
- `Save` は、追跡されていない集約を追加し、追跡済みの集約に対しては何もしない、という実装にできます。

## 論理ロック

データによっては、ユーザーが作業している間、長時間ロックしておく必要があります。ロックをどこに置くべきかは、その目的によって異なります。

| 目的 | 例 | 置き場所 |
|---|---|---|
| ロックが業務上の意味を持つ | 「承認中は編集できない」 | 集約の状態。`Lock` や `Unlock` などの操作を用意する |
| UI での同時編集を防ぐためのロック | 「Alice がこのレコードを編集中です」 | アプリケーション層の別のサービス |

どちらの場合も、ロックには有効期限を設け、管理者が解除できる手段を用意し、最後の防御線として楽観ロックを維持してください。
