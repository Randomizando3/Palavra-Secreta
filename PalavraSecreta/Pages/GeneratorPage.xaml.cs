using PalavraSecreta.Services;

namespace PalavraSecreta.Pages;

public partial class GeneratorPage : ContentPage
{
    private readonly LocalWordBankService _bank;

    public GeneratorPage(LocalWordBankService bank)
    {
        InitializeComponent();
        _bank = bank;

        ThemePicker.ItemsSource = new List<string> { "Animais", "Frutas", "Objetos", "Outros" };
        ThemePicker.SelectedIndex = 2;

        _ = RefreshAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _bank.EnsureBankAsync();

        var theme = (ThemePicker.SelectedItem as string) ?? "Objetos";
        var count = await _bank.GetCountAsync(theme);

        CountLabel.Text = $"Itens no tema '{theme}': {count}";
    }

    private async void OnAdd(object sender, EventArgs e)
    {
        var theme = (ThemePicker.SelectedItem as string) ?? "Outros";

        var baseW = (BaseEntry.Text ?? "").Trim();
        var revW = (RevealEntry.Text ?? "").Trim();

        // ? sempre vermelho
        const string color = "red";

        var (ok, msg) = await _bank.AddUserItemAsync(theme, baseW, revW, color);

        StatusLabel.Text = msg;

        if (ok)
        {
            BaseEntry.Text = "";
            RevealEntry.Text = "";
        }

        await RefreshAsync();
    }

    private async void OnGoGame(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//game");
    }
}
