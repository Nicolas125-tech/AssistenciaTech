with open('Controllers/AdminController.cs', 'r') as f:
    lines = f.readlines()

# Add the using statement at line 7
lines.insert(7, "using AssistenciaTech.ViewModels;\n")

# Re-read to find the Index method accurately after the insert
with open('Controllers/AdminController.cs', 'w') as f:
    f.writelines(lines)

with open('Controllers/AdminController.cs', 'r') as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    if "public async Task<IActionResult> Index(" in line:
        start_index = i
        break

end_index = start_index
brace_count = 0
found_first_brace = False

while end_index < len(lines):
    if '{' in lines[end_index]:
        brace_count += lines[end_index].count('{')
        found_first_brace = True
    if '}' in lines[end_index]:
        brace_count -= lines[end_index].count('}')

    if found_first_brace and brace_count == 0:
        break
    end_index += 1

new_method = """        public async Task<IActionResult> Index(string searchString, string statusFilter, int page = 1)
        {
            var viewModel = new AdminDashboardViewModel
            {
                SearchString = searchString,
                StatusFilter = statusFilter
            };

            if (TempData["ErroBanco"] != null)
            {
                viewModel.ErroBanco = TempData["ErroBanco"].ToString();
            }

            try
            {
                viewModel.DashboardData = await _facade.Dashboard.GetDashboardDataAsync(searchString, statusFilter, page);
                return View(viewModel);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR (Admin/Index)");
                viewModel.ErroBanco = "Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.";
                viewModel.DashboardData = new DashboardDto(); // Return empty dashboard to prevent view errors
                return View(viewModel);
            }
        }
"""

lines[start_index:end_index+1] = [new_method]

with open('Controllers/AdminController.cs', 'w') as f:
    f.writelines(lines)
