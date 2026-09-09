import sys
import subprocess

title = "🧪 Add unit tests for GerarXmlNfse in FaturamentosController"
body = """🎯 **What:**
Addressed the testing gap in `FaturamentosController` by adding comprehensive unit tests for the previously untested `GerarXmlNfse` endpoint.

📊 **Coverage:**
The following scenarios are now covered with automated tests using Moq and FluentAssertions:
- **Happy Path (`GerarXmlNfse_ValidFaturamento_ReturnsXmlFile`)**: Verifies that a valid `Faturamento` successfully invokes the `INfseXmlGeneratorService` and returns the expected XML payload as a `FileContentResult` with the `application/xml` content type.
- **Edge Case - Faturamento Not Found (`GerarXmlNfse_FaturamentoNotFound_ReturnsNotFound`)**: Verifies that attempting to retrieve an XML for a non-existent or invalid ID safely returns a `NotFoundObjectResult`.
- **Edge Case - OrdemServico Null (`GerarXmlNfse_OrdemServicoNull_ReturnsNotFound`)**: Confirms EF Core's inner-join behavior correctly returns `NotFoundObjectResult` when the dependent `OrdemServico` entity is missing.
- **Edge Case - Cliente Null (`GerarXmlNfse_ClienteNull_ReturnsNotFound`)**: Confirms EF Core's inner-join behavior correctly returns `NotFoundObjectResult` when the dependent `Cliente` entity is missing.

✨ **Result:**
Significant improvement in test coverage for critical business endpoints. The `GerarXmlNfse` logic is now fully verified against regressions.
"""

# write body to file
with open("pr_body.txt", "w") as f:
    f.write(body)
