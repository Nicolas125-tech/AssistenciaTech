🧪 [Testing Improvement] Edge case test for Warranty Return Alert

🎯 **What:** The testing gap addressed
This PR addresses the lack of test coverage for the edge case in `AdminController.Create` where a newly registered service order (Ordem de Serviço) matches an existing order by serial number within a 30-day window.

📊 **Coverage:** What scenarios are now tested
The test ensures that if a duplicated serial number is detected on an equipment that already gave entry to the shop in the past 30 days, the application sets the expected TempData attribute "AlertaGarantia".

✨ **Result:** The improvement in test coverage
Increased the robustness of the system by asserting that this specific workflow properly alerts the administrative user of a potential warranty return, preventing manual workarounds and ensuring TempData is correctly populated in an isolated manner.
