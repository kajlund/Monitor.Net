using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel(IConfiguration configuration) : PageModel
{
    public ApplicationDefinition[] Applications { get; private set; } = [];

    public void OnGet()
    {
        Applications = configuration
            .GetSection("Applications")
            .Get<ApplicationDefinition[]>()
            ?? [];
    }
}