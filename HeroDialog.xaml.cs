using System.Windows;
using DotaHeroes.Models;

namespace DotaHeroes;

public partial class HeroDialog : Window
{
    private int _editId;

    /// <summary>Созданный или обновлённый герой (устанавливается после «Сохранить»).</summary>
    public Hero? Hero { get; private set; }

    public HeroDialog()
    {
        InitializeComponent();
        UpdatePanelsVisibility();
        NameTextBox.Text = "";
        HealthTextBox.Text = "100";
        DamageTextBox.Text = "20";
    }

    /// <summary>Заполняет форму данными существующего героя (режим редактирования).</summary>
    public void SetHeroData(Hero hero)
    {
        Title = "Редактировать героя";
        _editId = hero.Id;

        NameTextBox.Text = hero.Name;
        HealthTextBox.Text = hero.Health.ToString();
        DamageTextBox.Text = hero.Damage.ToString();

        if (hero is Carry c)
        {
            CarryRadio.IsChecked = true;
            CritSlider.Value = c.CritChance;
            AgilityTextBox.Text = c.Agility.ToString();
        }
        else if (hero is Support s)
        {
            SupportRadio.IsChecked = true;
            ManaTextBox.Text = s.Mana.ToString();
            HealPowerTextBox.Text = s.HealPower.ToString();
        }
        UpdatePanelsVisibility();
    }

    private void HeroType_Changed(object sender, RoutedEventArgs e) => UpdatePanelsVisibility();

    private void UpdatePanelsVisibility()
    {
        // события могут сработать до создания всех элементов
        if (CarryPanel == null || SupportPanel == null || CarryRadio == null) return;

        bool carry = CarryRadio.IsChecked == true;
        CarryPanel.Visibility = carry ? Visibility.Visible : Visibility.Collapsed;
        SupportPanel.Visibility = carry ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CritSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CritValueText == null) return;
        CritValueText.Text = $"{(int)CritSlider.Value}%";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Warn("Введите имя героя.");
            return;
        }
        if (!TryInt(HealthTextBox.Text, "Здоровье", out int health)) return;
        if (!TryInt(DamageTextBox.Text, "Урон", out int damage)) return;

        if (CarryRadio.IsChecked == true)
        {
            if (!TryInt(AgilityTextBox.Text, "Ловкость", out int agility)) return;
            Hero = new Carry(_editId, name, health, damage, (int)CritSlider.Value, agility);
        }
        else
        {
            if (!TryInt(ManaTextBox.Text, "Мана", out int mana)) return;
            if (!TryInt(HealPowerTextBox.Text, "Сила лечения", out int heal)) return;
            Hero = new Support(_editId, name, health, damage, mana, heal);
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool TryInt(string text, string field, out int value)
    {
        if (int.TryParse(text, out value) && value >= 0) return true;
        Warn($"Поле «{field}» должно быть целым неотрицательным числом.");
        return false;
    }

    private static void Warn(string message)
        => MessageBox.Show(message, "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
}
