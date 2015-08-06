GlobalSettingsManager (settings writer/reader to/from database. Allows custom serializers and repositories.)
=============

**Usage examples:**
```csharp
public class MySettings : SelfManagedSettings<Settings>
{
   public override string Category { get { return "MySettings"; } }
   public DateTime Time { get; set; }
   public decimal Decimal { get; set; }
}

var repo = new SqlRepository("connectionString");
SimpleSettingsManager.DefaultSettingsManager = new SettingsManagerPeriodic(repo);
var settings = MySettings.Get(); //settings loads from repository and stays cached
settings.Save(); //settings are saved to repository
var task = manager.StartReadingTask(TimeSpan.FromSeconds(1), new CancelationTokenSource().Token); //periodically monitors repository for changes
settings.ChangeAndSave((s) => s.Decimal = 1); //changes value and saves to repository in single transaction (needed when periodic reading is enabled)

```