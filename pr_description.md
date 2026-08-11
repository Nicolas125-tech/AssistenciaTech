🧪 Add Unit Tests for PecasController.Create

🎯 **What:**
The CRUD logic in `PecasController`, specifically the `Create` POST method, was previously untested. This PR adds unit tests to ensure that the method correctly handles both valid and invalid models.

📊 **Coverage:**
- `Create_Post_ValidModel_ShouldAddPecaAndRedirectToIndex`: Verifies the happy path where a valid `Peca` model successfully adds a record to the database and redirects to the "Index" action.
- `Create_Post_InvalidModel_ShouldReturnViewWithModel_AndNotSaveToDb`: Verifies that if `ModelState` is invalid, the `Create` action returns the view with the provided invalid model and does NOT save it to the database.
- `Create_Get_ShouldReturnView`: Verifies that the initial GET request returns the expected `ViewResult`.

✨ **Result:**
Significant testing improvement. We now have a safety net for creating records in the Pecas entity, guaranteeing that core functionalities remain intact as we scale and refactor the application.
