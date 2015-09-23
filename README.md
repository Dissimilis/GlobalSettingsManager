GlobalSettingsManager
=============
Library for sharing settings across multiple projects. 
Currently it has implementation to use SQL Server for storage and communication. 

It should be easy to implement custom ISettingsRepository for using pub/sub or different databases.

Currently it has implementation for periodically reading repository/database to auto update settings values. 

**Usage examples:**
```csharp
public class MySettings : SettingsBase
{
   public override string Category { get { return "MySettings"; } }
   public DateTime Time { get; set; }
   public decimal Decimal { get; set; }
   public MySettings()
   {
      Decimal = 123m; //you can set default value
   }
}

var repo = new SqlRepository("connectionString");
var manager = new SettingsManagerPeriodic(repo); 
var settings = manager.Get<MySettings>(); //loads settings from repository keep then cached ()
manager.Save(settings); //settings are saved to repository
var task = manager.StartReadingTask(TimeSpan.FromSeconds(1), new CancelationTokenSource().Token); //periodically monitors repository for changes
manager.ChangeAndSave((s) => s.Decimal = 1, settings); //changes value and saves to repository in single transaction (needed when periodic reading is enabled)
```
**Setting custom serializer**
```csharp
//This lib uses TypeConverter for simple types (including DateTime, TimeSpan)
//For complex types it uses XmlSerializer by default.
//You would probably want to use Json.NET like this:
SettingsManager manager;
///<..>
manager.Converter.Serialize = JsonConvert.SerializeObject;
manager.Converter.Deserialize = JsonConvert.DeserializeObject;
```

**Other custom settings**
```csharp
SettingsManagerPeriodic manager;
///<..>
//Create settings in repository with default values if this no settings are found with matching category name
manager.AutoPersistOnCreate = true;
//Set to false if you don't want to get exception when invalid value in database is found and can't be assigned to property
manager.ThrowPropertySetException = false;
//Use this event to log such exception
manager.PropertyError += (sender, args) => { Console.WriteLine(args.Exception.ToString()); };
//Detects repeating errors and sets IsRepeating property in PeriodicReaderError event args for spamming prevention 
//Use this property to explicitly define for how long an exception should be considered as repeating (Default is 90 seconds)
manager.RepeatingErrorInterval = TimeSpan.FromSeconds(60);

//use this event to log exceptions from periodic task
manager.PeriodicReaderError += (sender, args) => { Console.WriteLine(args.Exception.ToString()); };
```