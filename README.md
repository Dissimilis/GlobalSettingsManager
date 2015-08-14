GlobalSettingsManager
=============
Libarary for sharing settings across multiple projects. 
Currently it has implementation to use SQL Server for storage and communication. 

It should be easy to implement custom ISettingsRepository for using pub/sub or different databases.

Currently it has implementation for periodically reading repository/database to auto update settings values. 

**Usage examples:**
```csharp
public class MySettings : SelfManagedSettings<MySettings>
{
   public override string Category { get { return "MySettings"; } }
   public DateTime Time { get; set; }
   public decimal Decimal { get; set; }
}

var repo = new SqlRepository("connectionString");
SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(repo); 
var settings = MySettings.Get(); //loads settings from repository keep then cached
settings.Save(); //settings are saved to repository
var task = manager.StartReadingTask(TimeSpan.FromSeconds(1), new CancelationTokenSource().Token); //periodically monitors repository for changes
settings.ChangeAndSave((s) => s.Decimal = 1); //changes value and saves to repository in single transaction (needed when periodic reading is enabled)

```
