# Cash register

The point of sale requires one open tenant cash session. Opening, supply, withdrawal and closing operations are protected by the session row version and create compact audit records. A cash sale creates a `CashMovement.Sale` inside the same transaction as the sale and inventory movements.

Endpoints are `/api/cash/current`, `/open`, `/supply`, `/withdraw` and `/close`, protected by `cash.view` and `cash.manage`. Closing records the counted value and a signed closing adjustment when it differs from the expected amount.
