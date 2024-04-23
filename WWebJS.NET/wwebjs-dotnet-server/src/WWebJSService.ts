import WAWebJS, { Client , ClientOptions,Events,LocalAuth,Message} from "whatsapp-web.js";
//import { ServerUnaryCall, handleCall, handleUnaryCall, handleBidiStreamingCall, sendUnaryData, ServerDuplexStream, handleServerStreamingCall, ServerWritableStream, ServiceError, status} from "grpc";
import internal from "stream";
import { mapEventEnumToString, mapStringToEventEnum } from "./grpcTypesHelper";
import * as messaged from "./generated/WWebJS_pb"
import path from "path"
import {  ClientEventType, DevGetLastMessageRequest, DevGetLastMessageResponse, DevGetMessagesRequest, DevGetMessagesResponse, ExitRequest, ExiResponse, InitClientRequest, InitClientResponse, PingRequest, PingResponse, SendMessageRequest, SendMessageResponse, WWebJSServiceOptions, WWebJsServiceServer } from "./generated/WWebJS";
import { Empty } from "./generated/google/protobuf/empty";
import {Message as MessageProto,  MessageContent } from "./generated/Message";
import { handleServerStreamingCall, handleUnaryCall , status, ServiceError} from "@grpc/grpc-js";


export class WWebJSServiceImpl  {

    
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
    
     public options: WWebJSServiceOptions;
     ping: handleUnaryCall<PingRequest, PingResponse> =(call,callback)=>{
        console.log("called ping with args ", call.request)
        callback(null, {text:call.request.text})
    }
    exit: handleUnaryCall<ExitRequest, ExitRequest> =(call,callback)=>{
        console.log("called exit with args ", call.request)
        callback(null, {force:true})
        process.exit(0);
    }
    devGetMessages: handleUnaryCall<DevGetMessagesRequest, DevGetMessagesResponse> =(call,callback)=>{
        console.log("called devGetMessages with arg ", call.request)
        

        callback(null, {messages:[{body:`test msg body ${call.request.contact?.phone}`}]})
    }
    devGetLastMessage: handleUnaryCall<DevGetLastMessageRequest, DevGetLastMessageResponse>=(call,callback)=>{
        console.log("called devGetLastMessage with arg ", call.request)


        this._initClientWithHandle("y",(eventName,...args)=>{
            var res = new proto.WWebJsService.InitClientResponse();
                res.setEventType(mapStringToEventEnum(eventName as Events));
                res.setEventArgsJson(JSON.stringify([...args]));
                console.log("sending json ",JSON.stringify([...args]) )
                if(eventName== mapEventEnumToString(ClientEventType.MESSAGE_CREATE)){
                    var msg = MessageProto.fromPartial({body:`message created on ${(args[0] as WAWebJS.Message).to}  `});
                    callback(null, {message:msg})
                }
        })
        return

        var msg = MessageProto.fromPartial({body:`test msg body ${call.request.contact?.number}`});
        console.log("sending ", {message:msg})
        callback(null, {message:msg})


    }
    initClient: handleServerStreamingCall<InitClientRequest, InitClientResponse>=(call)=>{
        console.log("called initClient with arg ", call.request)
        var req = call.request.clientCreationOptions
        const handle = req?.sessionFolder
        
        if(!handle){
            throw {code:status.INVALID_ARGUMENT,details:"handle is empty"} as ServiceError
        }
        this._initClientWithHandle(handle,(eventName,...args)=>{
            var res =InitClientResponse.create()
                res.eventType= mapStringToEventEnum(eventName as Events)  ;
                res.eventArgsJson=JSON.stringify([...args]);
                console.log("sending json ",JSON.stringify([...args]) )
                call.write(res)
        })
        /*setTimeout(()=>{
            var res =InitClientResponse.create()
                res.eventType= mapStringToEventEnum(Events.QR_RECEIVED);
                res.eventArgsJson=JSON.stringify(["testing write",1]);
                console.log("sending test ")
                call.write(res)
                call.end();
        },1000)
        return*/
        
        
    }
    sendMessage: handleUnaryCall<SendMessageRequest, SendMessageResponse>=(call,callback)=>{
        console.log("called sendMessage with arg ", call.request)

        try {
            const req = call.request;
            const res =  SendMessageResponse.create();

            const client = this.clients.get(req.clientHandle);
            if(client===undefined){
                throw new Error("no such client handle");
            }
            if(req.content ===undefined) throw new Error("content null")
            var content = req.content 

            console.log("sending message content: ", content);

            if(content.text===undefined) throw new Error("only text messages are supported")
            var optionsProto = req.options
            var sendOptions :WAWebJS.MessageSendOptions = {
                caption:optionsProto?.caption,
                media:optionsProto?.media,
            }  
            client.sendMessage(req.chatId,content.text,sendOptions)
            .then(msg=> {
                res.message= MessageProto.fromPartial({
                    ack:msg.ack,
                    body:msg.body,
                    author:msg.author,
                    from:msg.from,
                    id:{fromMe:msg.id.fromMe,id:msg.id.id,remote:msg.id.remote,serialized:msg.id._serialized},
                    to:msg.to
                })
                
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
    setOptions: handleUnaryCall<WWebJSServiceOptions, WWebJSServiceOptions>=(call,callback)=>{
        console.log("called setOptions with arg ", call.request)
        var opt  = call.request 
        this.options = {...this.options,...opt}
        console.log("updated options to: ",this.options)
        //todo validate
        callback(null, call.request)
    }

    

    _initClientWithHandle(handle:string,cb:(eventName:string,...evenArgs:any[])=>void){

        console.log(" options: ",this.options)

        console.log("creating client with handle ", handle)
        var cachePath = path.join(path.dirname(this.options.userDataDir),"wwebjs_cache_mi/")
        console.log("cachePath ", cachePath)

        const wwebVersion = "2.2412.54"//DEV NOTE: check wis/build notes for any issues with wawebJS
        let new_wp_wlient = new Client({
            authStrategy: new LocalAuth({ clientId: handle, dataPath: this.options.userDataDir }),
            puppeteer: {
                executablePath: this.options.chromeExecutablePath ,
                headless:this. options.headlessChromeMode,
                args: this.options.chromeArgs,
                timeout: 60000,
                
            },
            webVersionCache: { 
                type: 'remote',
                remotePath: `https://raw.githubusercontent.com/wppconnect-team/wa-version/main/html/${wwebVersion}.html`,
            },        
        })
        console.log("created client")
                
        new_wp_wlient.initialize();
        new_wp_wlient.on("message_create",qr=>{
            console.log("msgcreat")
            var res = InitClientResponse.fromPartial(
                {
                eventArgsJson:JSON.stringify([qr]),
                eventType:ClientEventType.QR_RECEIVED
            });
            //call.write(res)
        })

        for (const eventName of Object.values(Events)){
            console.log("registrede event ", eventName)
            new_wp_wlient.on(eventName,(a,b,c,d,e)=>{
                cb(eventName,a,b,c,d,e)
                
            })
        }
        this.clients.set(handle,new_wp_wlient)
    }

    
   
    

}