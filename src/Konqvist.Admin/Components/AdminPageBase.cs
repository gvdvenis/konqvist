using Microsoft.AspNetCore.Components;

namespace Konqvist.Admin.Components;

public abstract class AdminPageBase : ComponentBase
{
    protected bool IsBusy { get; private set; }

    protected async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
