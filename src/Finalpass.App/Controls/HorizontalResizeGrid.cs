using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Finalpass.App.Controls;

public sealed class HorizontalResizeGrid : Grid
{
    public HorizontalResizeGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
