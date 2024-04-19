import { Client , ClientOptions,Events,Message} from "whatsapp-web.js";
import { ServerUnaryCall, handleCall, handleUnaryCall, handleBidiStreamingCall, sendUnaryData, ServerDuplexStream, handleServerStreamingCall, ServerWritableStream} from "grpc";
import internal from "stream";
import { mapStringToEventEnum } from "./grpcTypesHelper";
import * as messaged from "./WWebJS_pb"
export type WWebJSServiceOptions = {
    ChromeExecutablePath:string,
    HeadlessChromeMode:boolean
}

export class WWebJSServiceImpl{


    /**
     * 
     * @param options can be updated later via SetOptions calls
     */
    constructor(options:WWebJSServiceOptions){
        this.clients = new Map<string,Client>()
        this.options = options;
    }

    /**
     * keys are used as session directory names and must consist of alphanumeric characters+hyphens
     */
    public clients: Map<string,Client>

    options: WWebJSServiceOptions;

    public setOptions(call:ServerUnaryCall<proto.WWebJsService.WWebJSServiceOptions>,callback:sendUnaryData<proto.google.protobuf.Empty>){
        this.options = {...this.options,...call.request.toObject()}
        //todo validate
        callback(null, new proto.google.protobuf.Empty())
    }
    /**
     * @type {handleServerStreamingCall}
     */
    public CreateClient(call: ServerWritableStream<proto.WWebJsService.InitClientRequest,proto.WWebJsService.InitClientResponse>){
        var client = new Client(call.request.getClientcreationoptions()?.toObject());
        
        client.initialize();
        client.on("qr",qr=>{
            console.log("qr")
            return;
            var res = new proto.WWebJsService.InitClientResponse();
            res.setEventtype(proto.WWebJsService.ClientEventType.QR_RECEIVED);
            res.setEventargsjson(JSON.stringify([qr]));
            call.write(res)
        })
        for (const eventName in Events){
            client.addListener(eventName,(a,b,c,d,e)=>{
                var res = new proto.WWebJsService.InitClientResponse();
                res.setEventtype(mapStringToEventEnum(eventName as Events));
                res.setEventargsjson(JSON.stringify([a,b,c,d,e]));
                call.write(res)
            })
        }
        
    }
    

    public SendMessage(call:ServerUnaryCall<proto.WWebJsService.SendMessageRequest>,callback:sendUnaryData<proto.WWebJsService.SendMessageResponse>){
        try {
            const req = call.request;
            const res = new proto.WWebJsService.SendMessageResponse();

            const client = this.clients.get(req.getClienthandle());
            if(client===undefined){
                throw new Error("no such client handle");
            }
            client.sendMessage(req.getChatid(),req.getContent().toObject(),req.getOptions()?.toObject())
            .then(msg=> {
                res.setMessage(new proto.WWebJsService.Message([msg]));
                callback(null,res);
            })
            .catch(err=>{
                callback(err,null)
            })
        } catch (err) {
            callback(err,null)
        }
        
        
    }

}