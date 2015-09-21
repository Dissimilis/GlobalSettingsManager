GlobalSettingsManager
=============
Library for sharing settings across multiple projects. 
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
   public MySettings()
   {
      Decimal = 123m; //you can set default value
   }
}

var repo = new SqlRepository("connectionString");
SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(repo); 
var settings = MySettings.Get(); //loads settings from repository keep then cached ()
settings.Save(); //settings are saved to repository
var task = manager.StartReadingTask(TimeSpan.FromSeconds(1), new CancelationTokenSource().Token); //periodically monitors repository for changes
settings.ChangeAndSave((s) => s.Decimal = 1); //changes value and saves to repository in single transaction (needed when periodic reading is enabled)
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
manager.PropertyError += (sender, args) => { Console.WriteLine(args.ExceptionObject.ToString()); };
//Set to true to prevent spamming errors when repository has invalid value
manager.ThrottlePropertyExceptions = true;

//use this event to log exceptions from running task
manager.PeriodicReaderError += (sender, args) => 
{ 
    Console.WriteLine(args.ExceptionObject.ToString());
    Console.WriteLine(args.IsRepeating); //use property IsRepeating to check whether occured exception has already been caught during a certain period of time to prevent spamming
};
```