🧪 Add comprehensive tests and null check for PdfGeneratorService

🎯 **What:**
- Added a `null` check on the `os` parameter in `PdfGeneratorService.GenerateOsPdf` to prevent unhandled `NullReferenceException` inside the PDF rendering delegate logic.
- Added tests to cover the edge cases and missing paths inside `GenerateOsPdf`.

📊 **Coverage:**
- `GenerateOsPdf_ThrowsArgumentNullException_WhenOsIsNull`: Tests that the service properly guards against `null` input by throwing `ArgumentNullException`.
- `GenerateOsPdf_HandlesExpiredWarranty_AndEntregueStatus`: Tests the condition where `Status` is `WorkflowStatus.Entregue` and `DataEntregaCliente` signifies an expired warranty (e.g. over 90 days), covering conditional text and color-styling branches inside the method.

✨ **Result:**
- Increased robustness of the PDF generation service.
- Improved unit test coverage for `PdfGeneratorService`.
