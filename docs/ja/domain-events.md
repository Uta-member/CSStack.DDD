# ドメインイベント

ドメインイベントとは、「会員が登録された」のように、業務上意味のある出来事が起きたという事実です。

## イベントの定義

```csharp
public sealed record MemberRegistered(DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : DomainEvent(OccurredAt);
```

`DomainEvent` は `IDomainEvent` を実装し、既定で `Guid.NewGuid()` により `EventId` を生成します。`OccurredAt` は引数で渡します。永続化したイベントを復元するときは、オブジェクト初期化子で `EventId` に保存済みの識別子を設定してください。`with` によるコピーも識別子を維持します。別の表現が必要な場合は、引き続き `IDomainEvent` を直接実装できます。

| プロパティ | 用途 |
|---|---|
| `EventId` | 一意な識別子。受信側はこれを使って重複配信を無視します。 |
| `OccurredAt` | イベントが発生した日時。 |
| 集約の識別子 | どの集約で起きたイベントか。 |
| 業務データ | 受信側がイベントに反応するために必要な情報。 |

ガイドライン：

- **イベント名は過去形にします。** 例：`MemberRegistered`、`EmailChanged`。
- **イベントは不変のレコードにします。** 事実は、起きた後に変わることはありません。
- **集約そのものを参照しないでください。** 集約は可変なので、受信側が必要とする値をコピーします。
- **発生日時は、ドメインの中で時計を読まずに引数で受け取ります。** 日時は呼び出し側（通常はアプリケーション層）が決めるため、ドメインの結果が実行時刻に左右されずテストしやすくなります。また、過去に起きた事実を記録すること（例えばデータの取り込み）もできます。

## イベントの記録

`AggregateRoot<TSelf, TId>` から派生した集約ルートは、`AddDomainEvent` でイベントを記録します。

```csharp
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
{
	email.Validate().Under(nameof(Email)).ThrowIfInvalid();
	Email = email;
	AddDomainEvent(new MemberEmailChanged(occurredAt, Id, email));
}
```

再構築用のファクトリではイベントを記録しないでください。再構築は過去の状態を復元するだけで、新たに何かが起きたわけではありません。

## イベントをすぐに配信しない理由

イベントは集約に記録され、変更がコミットされるときに後から配信されます。

1. 操作の後でトランザクションが失敗した場合、すぐに配信されたイベントは、実際には起きなかった変更を伝えてしまいます。
2. 集約がディスパッチャーに依存する必要がなくなります。集約は起きたことを記録するだけです。

## イベントの回収

インフラストラクチャは、Unit of Work に登録されたすべての集約に対して `PullDomainEvents()` を呼び出し、イベントを回収します。

```csharp
foreach (var aggregate in registeredAggregates)
{
	if (aggregate is IDomainEventSource source)
	{
		foreach (var domainEvent in source.PullDomainEvents())
		{
			// Outbox に書き込む
		}
	}
}
```

- `IDomainEventSource` は非ジェネリックなので、リフレクションを使わずに、どの型の集約からでもイベントを回収できます。
- `PullDomainEvents()` はイベントの取得とクリアを 1 回の操作で行うため、同じイベントを誤って二重に配信することはありません。
- `AggregateRoot<TSelf, TId>` は `PullDomainEvents()` を明示的に実装しているため、集約そのものからではなく `IDomainEventSource` 経由でだけ呼び出せます。これにより、アプリケーションサービスやドメインのコードがコミットの前にイベントを取り出してクリアし、Outbox に書き込まれなくなることを防ぎます。操作がどのイベントを発生させたかを確認するには（テストなど）、イベントをクリアしない `DomainEvents` を読み取ります。

## 配信のタイミング

| タイミング | 適した用途 | 注意点 |
|---|---|---|
| **Outbox パターン（推奨）** | 他の集約の更新（結果整合性）、メール送信などの副作用、他システムとの連携 | 配信は at-least-once（少なくとも 1 回）なので、受信側は重複配信を無視する必要がある |
| コミット前、同じトランザクション内 | 業務が即時の整合性を必要とし、意図して 1 つのトランザクションで複数の集約を変更する場合 | 受信側が失敗すると、元の変更もロールバックされる。トランザクションが大きくなる |
| コミット後に直接 | 推奨しない | 配信前にプロセスが停止するとイベントが失われる |

既定は Outbox パターンです。処理は 2 つのトランザクションに分かれます。

```
トランザクション 1（ユースケース）
  集約の更新 + Outbox へのイベントの書き込み → コミット
  ※ イベントへの反応はまだ実行されない

トランザクション 2（Outbox の発行プロセス。イベントごと）
  Outbox からイベントを取り出す → イベントに反応する → 反応による変更 + 処理済みの記録 → コミット
  ※ 失敗したらロールバックしてリトライする。トランザクション 1 の変更には影響しない
```

イベントへの反応が失敗しても、元のユースケースの変更は取り消されません。反応による変更は、リトライによって後から反映されます（結果整合性）。

トランザクション内で反応させると、そこで発生した例外が元の変更までロールバックします。たとえば、ポイント口座の開設に失敗すると、会員登録まで失敗します。業務がそれを求める場合にだけ、意図して選んでください。

## Unit of Work での Outbox への書き込み

`CommitAsync` は、登録された集約からイベントを回収し、集約と同じトランザクションで Outbox に書き込みます。

```csharp
public async Task CommitAsync(CancellationToken cancellationToken = default)
{
	var transaction = await GetTransactionAsync(cancellationToken);

	var domainEvents = _registeredAggregates.SelectMany(aggregate => aggregate.PullDomainEvents()).ToList();

	foreach (var work in _pending) await work(transaction, cancellationToken);
	await WriteOutboxAsync(domainEvents, transaction, cancellationToken); // 集約と同じトランザクションで書き込む
	cancellationToken.ThrowIfCancellationRequested();
	transaction.Commit();
}
```

- `_registeredAggregates` は `List<IDomainEventSource>` です。
- トランザクションがロールバックされた場合、集約の変更もイベントも永続化されません。失敗した Unit of Work を破棄し、その集約インスタンスも再利用しないでください。`PullDomainEvents()` によってイベントは既にクリアされています。再試行しても安全な操作は、新しいスコープで読み込みからユースケース全体をやり直します。`CommitAsync` だけを再試行したり、同じ集約インスタンスを別の Unit of Work で保存したりしないでください。接続断などでコミット結果が不明な場合は、永続化結果を確認するか、アプリケーション側の冪等性を確保してから再試行します。

## イベントへの反応

ツールキットは、イベントに反応するハンドラーのインターフェースや、ハンドラーを解決して呼び出すディスパッチャーを提供しません。ハンドラーをどの単位で、どのトランザクションで、どう再試行して呼び出すかは、アプリケーションの構成によって変わるためです。自作の仕組みや、MediatR、Wolverine、MassTransit などのライブラリを使ってください。

どの仕組みを使う場合も、次の点を守ってください。

- **Outbox を監視して配信するのは、インフラストラクチャ層の Outbox リレーです。** ホストが `BackgroundService` として、または独立した Worker プロセスとして常駐させます。集約を保存したアプリケーションサービスやプレゼンテーション層が配信するわけではありません。
- **イベントごとに、元の変更とは別のスコープ・Unit of Work・トランザクションで反応します。** 反応による変更と「処理済み」の記録は、同じトランザクションでコミットします。
- **反応は冪等にします。** イベントは少なくとも 1 回届きます。データベースの変更は、処理済みの記録と同じトランザクションでコミットすれば二重には反映されませんが、業務上のキーで存在を確認し、一意制約を最後の防御線にしておくと安全です。メール送信のような外部への副作用は、その後のコミットが失敗すると繰り返されるため、`EventId` を使って重複を無視してください。
- **失敗したらリトライし、上限を超えたらデッドレターにします。**

次のスケッチは、Outbox リレーに必要な部分を示したものです。完全な実装ではありません。

```csharp
internal sealed class OutboxRelay(IServiceScopeFactory scopes, OutboxStore outbox) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			foreach (var record in await outbox.FetchPendingAsync(100, stoppingToken))
			{
				try
				{
					// イベントごとにスコープを作るので、Unit of Work とトランザクションも元の変更とは別になる
					using var scope = scopes.CreateScope();
					var unitOfWork = scope.ServiceProvider.GetRequiredService<DapperUnitOfWork>();

					await ReactAsync(scope.ServiceProvider, record.Deserialize(), stoppingToken); // ハンドラーの呼び出し方はアプリケーションが決める
					unitOfWork.Enqueue((transaction, cancellationToken) => outbox.MarkProcessedAsync(record.Id, transaction, cancellationToken));
					await unitOfWork.CommitAsync(stoppingToken); // 反応による変更と処理済みの記録を一緒にコミットする
				}
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					await outbox.RecordFailureAsync(record.Id, exception, stoppingToken); // 試行回数を増やし、上限を超えたらデッドレターにする
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}
}
```

- 反応の中で別の集約を保存すると、そのイベントは同じ `CommitAsync` で Outbox に書き込まれ、次の周回で配信されます。イベントの連鎖も、1 段ずつ別のトランザクションで処理されます。
- 1 つのイベントに複数のハンドラーがあり、それらが 1 つのトランザクションを共有する場合、1 つが失敗するとすべてロールバックされ、まとめてリトライされます。ハンドラーごとに独立させたい場合は、Outbox のレコードをハンドラー（購読者）ごとに分けるか、メッセージブローカーで購読者ごとにキューを分けてください。
- リレーを複数のインスタンスで動かす場合は、同じレコードを二重に処理しないよう、取り出すときにロックしてください（`SELECT ... FOR UPDATE SKIP LOCKED`、SQL Server では `WITH (UPDLOCK, READPAST)`）。
- メッセージブローカーを使う場合、リレーは Outbox のレコードをブローカーに送るだけです。ブローカーから受信するコンシューマーが、同じ手順（スコープの作成、反応、コミット）で反応します。

### トランザクション内での反応（即時の整合性が必要な場合）

業務が即時の整合性を必要とする場合は、`CommitAsync` の中で、コミットの前に反応させることもできます。

- 反応の中で別の集約を保存すると、新しいイベントが発生します。イベントがなくなるまで、回収と反応を繰り返してください。互いに発生し合うイベントで無限にループしないよう、繰り返しの回数に上限を設けてください。
- 反応が例外をスローすると、元の変更もロールバックされます。
- ここで反応させるのは、即時の整合性が必要なものだけにしてください。結果整合性で処理するものは、これまでどおり Outbox に書き込みます。

## イベントのメタデータ

操作を行ったのは誰か、どのリクエストが原因か、といった情報はメタデータであり、業務上の事実の一部ではありません。すべてのイベントに追加するのではなく、Outbox に書き込むときにエンベロープに付加してください。

```
Outbox レコード
├─ エンベロープ（インフラストラクチャが付加）：EventType, ActorId, CorrelationId, ...
└─ 本体（ドメインが作成）：MemberRegistered { MemberId, Email, ... }
```

「誰が」が業務上の意味を持つ場合（たとえば承認者）は、イベント自体に含めてください。[エンティティと集約](entities-and-aggregates.md) を参照してください。

## ドメインイベントと統合イベント

ドメインイベントは同じプロセス内で使われるため、値オブジェクトを含めてもかまいません。一方、他システムに送るイベントは、そのシステムとの契約です。プリミティブ値だけを含む別の統合イベント型に変換し、ドメインモデルの変更が契約を壊さないようにしてください。

## 複数の集約

一般的なガイドラインは、**1 つのトランザクションで変更する集約は 1 つ**にとどめ、他の集約はドメインイベントを通じて更新する（結果整合性）というものです。このとき、他の集約を更新するハンドラーは、Outbox から元の変更とは別のトランザクションで実行します（[配信のタイミング](#配信のタイミング) を参照）。Unit of Work を使えば 1 つのトランザクションで複数の集約を変更することも可能で、業務が即時の整合性を必要とする場合には妥当な選択ですが、意図して選ぶようにしてください。その場合、どちらかの集約の変更が失敗すると、すべての変更がロールバックされます。
