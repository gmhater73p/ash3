using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.ArsTurdgeBop {
    internal enum NotificationType {
        NewMission,
        NewMissionCritical,
        FailedMission,
        FailedMissionCritical,
        CompletedMission,
        NetworkConnect,
        NetworkDisconnect,
        NetworkInfo,
        ChatMessage,
        Star,
        NetworkDisconnectCritical,
        ScienceFlask
    }
}
