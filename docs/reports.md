# Reports

`GET /api/reports` consolidates sales, revenue, average ticket, payment methods, top products, current and low inventory, stock movements and received purchases for a requested period.

`GET /api/reports/export?format=excel|pdf` generates files on demand and requires `reports.export`. Excel uses license-free SpreadsheetML and PDF uses the internal minimal document writer; neither adds a commercial dependency nor stores generated files.
