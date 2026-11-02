using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailNotificationWirhOverandInterface
{
    public interface INotification
    {
        public bool SendNotification(string to, string title, string message1);
    }
}
