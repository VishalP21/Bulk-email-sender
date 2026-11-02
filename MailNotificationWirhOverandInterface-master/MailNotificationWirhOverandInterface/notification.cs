using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailNotificationWirhOverandInterface
{
    public class notification : IEmailnotification, Ismsnotification
    {
        void IEmailnotification.SendNotification(string to, string messge)
        {
            Console.WriteLine("this is email");
        }

        void Ismsnotification.SendNotification(string to, string messge)
        {
            Console.WriteLine("this is sms");
        }
    }
}
