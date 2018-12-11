using WitFX.Backend.Infrastructure;
using WitFX.MT4.Server.cls;

namespace WitFX.MT4.Server.Events
{
    [Injectable]
    public interface IMessageEvents
    {
        //void SendAsFile(GenericFileDataResponse fileHeader, object fileData);
        void SentDataUsingLoginID(object msg, MessageTypeID msgType, int loginID);
        //void SentDataUsingSocketID(object msg, MessageTypeID msgType, uint socketID);
        //void SentDataToAll(object msg, MessageTypeID msgType);
    }
}
