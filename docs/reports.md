# Reports

`GET /api/reports` consolidates sales, revenue, average ticket, payment methods, top products, current and low inventory, detailed stock movements and purchases for a requested period. The two detailed operational lists return at most the 100 most recent rows while their headline totals consider the complete filtered period.

`GET /api/reports/export?format=excel|pdf` generates files on demand and requires `reports.export`. Excel uses license-free SpreadsheetML with separate summary, payment, product, purchase, movement and inventory worksheets. PDF uses the internal minimal document writer for a concise managerial handout. Neither adds a commercial dependency nor stores generated files.
