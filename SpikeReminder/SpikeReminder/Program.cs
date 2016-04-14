using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SpikeReminder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpikeReminder
{
    class Program
    {
        private static IMongoClient _client;
        private static IMongoDatabase _db;

        static void Main(string[] args)
        {
            _client = new MongoClient();
            _client.DropDatabase("TestReminder");
            _db = _client.GetDatabase("TestReminder");

            InitializeDatabase();
            ReminderService.Start(_db, new FakeSmtpService());
        }

        private static void InitializeDatabase()
        {
            _db.CreateCollection("Users");

            var now = DateTime.Now;
            var seed = 9;
            var random = new Random(seed);

            for (var i = 0; i < 2; i++)
            {
                var user = new User(string.Concat("User", i), "user@mymail.com");
                var minute = random.Next(7, 10);
                var e = new Event(string.Concat("Event", i), now.AddMinutes(minute));
                e.AddRemind(now.AddMinutes(2));
                e.AddRemind(now.AddMinutes(4));
                e.AddRemind(now.AddMinutes(6));
                user.AddEvent(e);
                _db.GetCollection<User>("Users").InsertOne(user);
            }
        }
    }

    class ReminderService
    {
        private static bool launched = false;
        private readonly IMongoDatabase _db;
        private readonly ISmtpService _smtpService;

        private ReminderService(IMongoDatabase db, ISmtpService smtpService)
        {
            _db = db;
            _smtpService = smtpService;
        }

        private void Execute()
        {
            for (; ; )
            {
                try
                {
                    var now = DateTime.Now;
                    var users = _db.GetCollection<User>("Users")
                        .Find(x => x.HasUnexecutedReminds)
                        .ToList();

                    double nextRemind = 3600000; // Default pause 1 hour
                    var diff = TimeSpan.Zero;

                    foreach (var user in users)
                    {
                        foreach (var e in user.Events.Where(x => x.HasUnexecutedReminds).ToList())
                        {
                            foreach (var r in e.Reminds.Where(r => r.Sent == false).ToList())
                            {
                                if (now > r.Date)
                                {
                                    _smtpService.SendMail(user, e, r);
                                    r.Sent = true;
                                    
                                    // Update
                                    var filter = Builders<User>.Filter.Eq("_id", user._id);
                                    _db.GetCollection<User>("Users")
                                        .ReplaceOne(filter, user);
                                }
                                else
                                {
                                    diff = r.Date - now;
                                    nextRemind = diff.TotalMilliseconds < nextRemind ? diff.TotalMilliseconds : nextRemind;
                                }
                            }
                        }

                    }
                    if (nextRemind == 3600000)
                    {
                        Thread.CurrentThread.Abort();
                    }
                    else
                    {
                        Console.WriteLine("Pause {0}", nextRemind);
                        Thread.Sleep((int)nextRemind);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public static void Start(IMongoDatabase db, ISmtpService smtpService)
        {
            if (!launched)
            {
                launched = true;
                new Thread(() =>
                {
                    var reminder = new ReminderService(db, smtpService);
                    reminder.Execute();
                }).Start();
            }
        }
    }

    class User
    {
        public MongoDB.Bson.ObjectId _id { get; set; }

        [BsonRequired]
        public string Name { get; private set; }

        [BsonRequired]
        public string Email { get; private set; }

        [BsonRequired]
        public HashSet<Event> Events { get; private set; }

        public User(string name, string email)
        {
            Name = name;
            Email = email;
            Events = new HashSet<Event>();
        }

        public bool HasUnexecutedReminds
        {
            get
            {
                return Events.Any(x => x.HasUnexecutedReminds);
            }
            set
            {
            }
        }

        public void AddEvent(Event e)
        {
            Events.Add(e);
        }
    }

    class Event
    {
        [BsonRequired]
        public string Name { get; private set; }

        [BsonRequired]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime Date { get; private set; }

        public HashSet<Remind> Reminds { get; private set; }

        public bool HasUnexecutedReminds
        {
            get
            {
                return Reminds.Any(x => x.Sent == false);
            }
            set
            {
            }
        }

        public Event(string name, DateTime date)
        {
            Name = name;
            Date = date;
            Reminds = new HashSet<Remind>();
        }

        public void AddRemind(DateTime d)
        {
            if (d < Date && d > DateTime.Now)
            {
                Reminds.Add(new Remind(d));
            }
        }

        public override bool Equals(object obj)
        {
            var e = obj as Event;
            return e != null ? e.Name == Name : false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    class Remind
    {
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime Date { get; set; }

        public bool Sent { get; set; }

        public Remind(DateTime date)
        {
            Date = date;
        }

        public override bool Equals(object obj)
        {
            var r = obj as Remind;
            return r != null ? r.Date == Date : false;
        }

        public override int GetHashCode()
        {
            return Date.GetHashCode();
        }
    }
}
