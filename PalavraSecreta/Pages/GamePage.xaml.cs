using PalavraSecreta.Services;

namespace PalavraSecreta.Pages;

public partial class GamePage : ContentPage
{
    private readonly LocalWordBankService _bank;
    private readonly Models.WordBoardDrawable _drawable = new();

    private string _selectedTheme = "Aleatório";
    private int _selectedCount = 4;

    public GamePage(LocalWordBankService bank)
    {
        InitializeComponent();
        
        _bank = bank;

        ThemePicker.ItemsSource = new List<string> { "Aleatório", "Animais", "Frutas", "Objetos", "Outros" };
        ThemePicker.SelectedIndex = 0;

        CountPicker.ItemsSource = new List<string> { "4", "8", "12" };
        CountPicker.SelectedIndex = 0;

        BoardView.Drawable = _drawable;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPan;
        BoardView.GestureRecognizers.Add(pan);

        LensLabel.Text = "Lupa: red";
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        _selectedTheme = (ThemePicker.SelectedItem as string) ?? "Aleatório";
        // não gera aqui
    }

    private void OnCountChanged(object sender, EventArgs e)
    {
        if (CountPicker.SelectedItem is string s && int.TryParse(s, out var n))
            _selectedCount = n;
        else
            _selectedCount = 4;

        // não gera aqui
    }

    private async void OnStart(object sender, EventArgs e)
    {
        await _bank.EnsureBankAsync();

        var items = await _bank.GetRandomSetAsync(_selectedTheme, _selectedCount);

        if (items.Count == 0)
        {
            await DisplayAlert("Banco vazio",
                "Não há itens válidos no banco para esse tema. Cadastre mais itens no Gerar.",
                "OK");
            return;
        }

        if (items.Count < _selectedCount)
        {
            await DisplayAlert("Poucos itens no tema",
                $"O tema '{_selectedTheme}' tem apenas {items.Count} item(ns) válido(s). " +
                $"Para gerar {_selectedCount}, adicione mais itens no Gerar.",
                "OK");
        }

        _drawable.Load(items);

        // coloca lupa no centro do board
        var w = (float)BoardView.Width;
        var h = (float)BoardView.Height;
        if (w > 0 && h > 0)
            _drawable.SetLensPosition(w / 2f, h / 2f);

        BoardView.Invalidate();
        LensLabel.Text = $"Mural gerado: {items.Count} palavra(s).";
    }

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _drawable.BeginPan();
                break;

            case GestureStatus.Running:
                _drawable.MoveLensBy((float)e.TotalX, (float)e.TotalY);
                BoardView.Invalidate();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _drawable.CommitPan();
                break;
        }
    }
}
