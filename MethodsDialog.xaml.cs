using System.Windows;
using System.Windows.Controls;
using DotaHeroes.Data;
using DotaHeroes.Data.Entities;
using DotaHeroes.Models;
using Microsoft.EntityFrameworkCore;

namespace DotaHeroes;

/// <summary>
/// Диалог вызова методов классов Hero / Carry / Support над героями из базы данных.
/// Методы, меняющие состояние героя, сохраняют результат обратно в БД.
/// </summary>
public partial class MethodsDialog : Window
{
    public MethodsDialog(int? selectedHeroId = null)
    {
        InitializeComponent();
        if (selectedHeroId != null)
            IdTextBox.Text = selectedHeroId.ToString();
    }

    // ---------- вспомогательные ----------

    private void Log(string text) => OutputTextBox.AppendText(text + Environment.NewLine);

    private bool TryReadInt(TextBox box, string field, out int value)
    {
        if (int.TryParse(box.Text.Trim(), out value)) return true;
        MessageBox.Show($"Поле «{field}» должно быть целым числом.", "Ошибка ввода",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private static IQueryable<HeroEntity> WithData(DotaDbContext db)
        => db.Heroes.Include(h => h.CarryData).Include(h => h.SupportData);

    /// <summary>
    /// Загружает героя из БД, применяет к нему действие и (если save = true) сохраняет изменения.
    /// </summary>
    private async Task RunOnHero(Func<Hero, string> action, bool save)
    {
        if (!TryReadInt(IdTextBox, "ID героя", out int id)) return;

        try
        {
            using var db = new DotaDbContext();
            var entity = await WithData(db).FirstOrDefaultAsync(h => h.Id == id);
            if (entity == null)
            {
                Log($"Герой с ID {id} не найден.");
                return;
            }

            Hero hero = HeroMapper.ToModel(entity);
            Log(action(hero));

            if (save)
            {
                HeroMapper.UpdateEntity(entity, hero);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex) { Log("Ошибка: " + ex.Message); }
    }

    // ---------- методы Hero ----------

    private async void GetInfo_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IdTextBox.Text))
        {
            try
            {
                using var db = new DotaDbContext();
                var all = await WithData(db).AsNoTracking().OrderBy(h => h.Id).ToListAsync();
                if (all.Count == 0) Log("В базе нет героев.");
                foreach (var entity in all)
                    Log($"#{entity.Id} " + HeroMapper.ToModel(entity).GetInfo());
            }
            catch (Exception ex) { Log("Ошибка: " + ex.Message); }
            return;
        }

        await RunOnHero(h => $"#{h.Id} " + h.GetInfo(), save: false);
    }

    private async void Attack_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(IdTextBox, "ID героя", out int attackerId)) return;
        if (!TryReadInt(TargetTextBox, "ID цели", out int targetId)) return;

        if (attackerId == targetId)
        {
            Log("Герой не может атаковать сам себя.");
            return;
        }

        try
        {
            using var db = new DotaDbContext();
            var attackerEntity = await WithData(db).FirstOrDefaultAsync(h => h.Id == attackerId);
            var targetEntity = await WithData(db).FirstOrDefaultAsync(h => h.Id == targetId);

            if (attackerEntity == null || targetEntity == null)
            {
                Log("Атакующий или цель не найдены.");
                return;
            }

            Hero attacker = HeroMapper.ToModel(attackerEntity);
            Hero target = HeroMapper.ToModel(targetEntity);

            // полиморфный вызов: выберется Carry.Attack или Support.Attack
            Log(attacker.Attack(target));

            HeroMapper.UpdateEntity(targetEntity, target);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { Log("Ошибка: " + ex.Message); }
    }

    private async void UseAbility_Click(object sender, RoutedEventArgs e)
        => await RunOnHero(h => h.UseAbility(), save: true);

    private async void TakeDamage_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(AmountTextBox, "Значение", out int amount)) return;
        await RunOnHero(h => h.TakeDamage(amount), save: true);
    }

    private async void IsAlive_Click(object sender, RoutedEventArgs e)
        => await RunOnHero(h => $"{h.Name}: IsAlive() = {(h.IsAlive() ? "true (жив)" : "false (погиб)")}", save: false);

    // ---------- методы Carry ----------

    private async void CanCriticalHit_Click(object sender, RoutedEventArgs e)
        => await RunOnHero(h => h is Carry c
            ? $"{c.Name}: CanCriticalHit() = {c.CanCriticalHit().ToString().ToLower()}"
            : OnlyFor(h, "Carry"), save: false);

    private async void IncreaseAgility_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(AmountTextBox, "Значение", out int amount)) return;
        await RunOnHero(h => h is Carry c ? c.IncreaseAgility(amount) : OnlyFor(h, "Carry"), save: true);
    }

    // ---------- методы Support ----------

    private async void HasEnoughMana_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(AmountTextBox, "Значение", out int cost)) return;
        await RunOnHero(h => h is Support s
            ? $"{s.Name}: HasEnoughMana({cost}) = {s.HasEnoughMana(cost).ToString().ToLower()} (мана: {s.Mana})"
            : OnlyFor(h, "Support"), save: false);
    }

    private async void RegenerateMana_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(AmountTextBox, "Значение", out int amount)) return;
        await RunOnHero(h => h is Support s ? s.RegenerateMana(amount) : OnlyFor(h, "Support"), save: true);
    }

    private static string OnlyFor(Hero h, string role)
        => $"Метод доступен только для {role}. {h.Name} — {h.RoleName}.";

    // ---------- кнопки окна ----------

    private void ClearOutput_Click(object sender, RoutedEventArgs e) => OutputTextBox.Clear();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
