using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpikeReminder
{
    class FakeSmtpService : ISmtpService
    {
        private readonly StringBuilder _strb;

        public FakeSmtpService()
        {
            File.Delete(@".\Reminders.txt");
            _strb = new StringBuilder();
        }

        public void SendMail(User user, Event e, Remind r)
        {
            _strb.AppendLine(string.Format("Rappel envoyé à {0} concernant l'utilisateur {1} pour l'évènement {2} débutant à {3}", r.Date.ToString(), user.Name, e.Name, e.Date.ToString()));
        }

        ~FakeSmtpService()
        {
            File.AppendAllText(@".\Reminders.txt", _strb.ToString());
        }
    }
}
