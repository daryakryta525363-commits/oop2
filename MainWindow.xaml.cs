using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DotaHeroes.Data;
using DotaHeroes.Models;
using Microsoft.EntityFrameworkCore;

namespace DotaHeroes;

public partial class MainWindow : Window
{
    public ObservableCollection<Hero> Heroes { get; set; } = new();
    public ObservableCollection<Hero> FilteredHeroes { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        try
        {
            using var db = new DotaDbContext();
            await db.Database.EnsureCreatedAsync();   // создаёт БД и таблицы при первом запуске
            await LoadHeroes();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка подключения к базе данных";
            MessageBox.Show("Не удалось подключиться к PostgreSQL:\n" + ex.Message +
                            "\n\nПроверьте строку подключения в Data/DotaDbContext.cs",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------- READ ----------

    private async Task LoadHeroes()
    {
        using var db = new DotaDbContext();
        var entities = await db.Heroes
            .Include(h => h.CarryData)
            .Include(h => h.SupportData)
            .AsNoTracking()
            .OrderBy(h => h.Id)
            .ToListAsync();

        Heroes.Clear();
        foreach (var entity in entities)
            Heroes.Add(HeroMapper.ToModel(entity));

        FilterHeroes();
        StatusText.Text = $"Героев в базе: {Heroes.Count}";
    }

    private void FilterHeroes()
    {
        string query = SearchTextBox.Text.Trim().ToLower();

        var result = string.IsNullOrEmpty(query)
            ? Heroes
            : Heroes.Where(h => h.Id.ToString().Contains(query)
                                || h.Name.ToLower().Contains(query)
                                || h.RoleName.ToLower().Contains(query));

        FilteredHeroes.Clear();
        foreach (var hero in result)
            FilteredHeroes.Add(hero);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => FilterHeroes();

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Clear();
        FilterHeroes();
    }

    // ---------- CREATE ----------

    private async void AddHero_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HeroDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Hero != null)
        {
            try
            {
                using var db = new DotaDbContext();
                db.Heroes.Add(HeroMapper.ToEntity(dialog.Hero));
                await db.SaveChangesAsync();
                await LoadHeroes();
            }
            catch (Exception ex) { ShowError(ex); }
        }
    }

    // ---------- UPDATE ----------

    private async void EditHero_Click(object sender, RoutedEventArgs e)
    {
        if (HeroesGrid.SelectedItem is not Hero selected)
        {
            MessageBox.Show("Выберите героя в таблице.", "Редактирование");
            return;
        }

        var dialog = new HeroDialog { Owner = this };
        dialog.SetHeroData(selected);

        if (dialog.ShowDialog() == true && dialog.Hero != null)
        {
            try
            {
                using var db = new DotaDbContext();
                var entity = await db.Heroes
                    .Include(h => h.CarryData)
                    .Include(h => h.SupportData)
                    .FirstOrDefaultAsync(h => h.Id == selected.Id);

                if (entity != null)
                {
                    HeroMapper.UpdateEntity(entity, dialog.Hero);
                    await db.SaveChangesAsync();
                    await LoadHeroes();
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }
    }

    // ---------- DELETE ----------

    private async void DeleteHero_Click(object sender, RoutedEventArgs e)
    {
        if (HeroesGrid.SelectedItem is not Hero selected)
        {
            MessageBox.Show("Выберите героя в таблице.", "Удаление");
            return;
        }

        var result = MessageBox.Show(
            $"Вы уверены, что хотите удалить героя «{selected.Name}»?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            using var db = new DotaDbContext();
            var entity = await db.Heroes.FirstOrDefaultAsync(h => h.Id == selected.Id);
            if (entity != null)
            {
                db.Heroes.Remove(entity);          // дочерняя запись удалится каскадно
                await db.SaveChangesAsync();
                await LoadHeroes();
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------- прочее ----------

    private async void RefreshHeroes_Click(object sender, RoutedEventArgs e)
    {
        try { await LoadHeroes(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void Methods_Click(object sender, RoutedEventArgs e)
    {
        int? selectedId = (HeroesGrid.SelectedItem as Hero)?.Id;
        var dialog = new MethodsDialog(selectedId) { Owner = this };
        dialog.ShowDialog();

        // методы могли изменить данные героев
        try { await LoadHeroes(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void ShowError(Exception ex)
        => MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
}
