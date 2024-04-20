import { Client , ClientOptions,Events,LocalAuth,Message} from "whatsapp-web.js";
import { ServerUnaryCall, handleCall, handleUnaryCall, handleBidiStreamingCall, sendUnaryData, ServerDuplexStream, handleServerStreamingCall, ServerWritableStream, ServiceError, status} from "grpc";
import internal from "stream";
import { mapStringToEventEnum } from "./grpcTypesHelper";
import * as messaged from "./generated/WWebJS_pb"
import path from "path"
import { WWebJSServiceOptions } from "./generated/WWebJS";
import { Empty } from "./generated/google/protobuf/empty";
import { MessageContent } from "./generated/Message";


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

    public setOptions(call:ServerUnaryCall<proto.WWebJsService.WWebJSServiceOptions>,callback:sendUnaryData<WWebJSServiceOptions>){
        var opt :WWebJSServiceOptions = call.request.toObject() as WWebJSServiceOptions;
        this.options = {...this.options,...opt}
        console.log("updated options to: ",this.options)
        //todo validate
        callback(null, call.request)
    }
    /**
     * @type {handleServerStreamingCall}
     */
    public InitClient(call: ServerWritableStream<proto.WWebJsService.InitClientRequest,proto.WWebJsService.InitClientResponse>){
        
        var req = call.request.getClientCreationOptions()
        const handle = req?.getSessionFolder();
        console.log(" options: ",this.options)

        if(!handle){
            throw {code:status.INVALID_ARGUMENT,details:"handle is empty"} as ServiceError
        }
        console.log("creating client with handle ", handle)
        var cachePath = path.join(path.dirname(this.options.userDataDir),"wwebjs_cache_mi/")
        console.log("cachePath ", cachePath)

        let new_wp_wlient = new Client({
            authStrategy: new LocalAuth({ clientId: handle, dataPath: this.options.userDataDir }),
            puppeteer: {
                executablePath: this.options.chromeExecutablePath ,
                headless:this. options.headlessChromeMode,
                args: this.options.chromeArgs,
                timeout: 60000,
                
            },
            webVersionCache:{type:'local', path: cachePath }
        })
        console.log("created client")
                
        new_wp_wlient.initialize();
        new_wp_wlient.on("message_create",qr=>{
            console.log("msgcreat")
            var res = new proto.WWebJsService.InitClientResponse();
            res.setEventType(proto.WWebJsService.ClientEventType.QR_RECEIVED);
            res.setEventArgsJson(JSON.stringify([qr]));
            call.write(res)
        })

        for (const eventName of Object.values(Events)){
            console.log("registrede event ", eventName)
            new_wp_wlient.on(eventName,(a,b,c,d,e)=>{
                var res = new proto.WWebJsService.InitClientResponse();
                res.setEventType(mapStringToEventEnum(eventName as Events));
                res.setEventArgsJson(JSON.stringify([a,b,c,d,e]));
                console.log("sending json ",JSON.stringify([a,b,c,d,e]) )
                //call.write(res)
            })
        }
        
    }
    

    public SendMessage(call:ServerUnaryCall<proto.WWebJsService.SendMessageRequest>,callback:sendUnaryData<proto.WWebJsService.SendMessageResponse>){
        try {
            const req = call.request;
            const res = new proto.WWebJsService.SendMessageResponse();

            const client = this.clients.get(req.getClientHandle());
            if(client===undefined){
                throw new Error("no such client handle");
            }
            var content: MessageContent = req.getContent()?.toObject() as MessageContent;
            console.log("sending message content: ", content);
            client.sendMessage(req.getChatId(),content,req.getOptions()?.toObject())
            .then(msg=> {
                res.setMessage(new proto.WWebJsService.Message([msg]));
                callback(null,res);
            })
            .catch(err=>{
                callback(err,null)
            })
        } catch (err :any ) {
            callback({
                code: status.UNKNOWN,
                details: err.message,
                message : err.message,
                name:""
            },null)
        }
        
        
    }

}