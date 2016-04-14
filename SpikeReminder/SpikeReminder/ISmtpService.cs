using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpikeReminder
{
    interface ISmtpService
    {
        void SendMail(User user, Event e, Remind r);
    }
}
