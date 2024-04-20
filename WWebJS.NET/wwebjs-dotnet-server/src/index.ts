//@ts-check
import { NamedPipeServer } from "grpc-js-namedpipes";

import WAWebJS from "whatsapp-web.js";
import * as services from "./generated/WWebJS_grpc_pb";
import messages from "./generated/WWebJS_pb";
import {sendUnaryData,handleUnaryCall} from "@grpc/grpc-js";
const VERSION = require('../package.json').version;
import { WWebJSServiceImpl } from "./WWebJSService";
if(process.argv.length<3){
    throw new Error("expected at 1 or more arguments")
}
var NAMED_PIPE_NAME = process.argv[2];
console.log("node: "+process.version)


const devGetLastMessage: handleUnaryCall<proto.WWebJsService.DevGetLastMessageRequest,proto.WWebJsService.DevGetLastMessageResponse> =
 (call,callback)=>{
    var response = new proto.WWebJsService.DevGetLastMessageResponse();
    var lastMsg = new proto.WWebJsService.Message();
    lastMsg.setBody("message from wds")
    
    response.setMessage(lastMsg)
    callback(null,response);
}

var serviceImpl = new WWebJSServiceImpl({
    chromeExecutablePath:"null", headlessChromeMode:false,
    chromeArgs:[],
    userDataDir:"",
    verbose : true
});

var l = new NamedPipeServer(NAMED_PIPE_NAME)

l.addService(services.WWebJsServiceService, 
    {
        initClient:serviceImpl.InitClient,
        setOptions:serviceImpl.setOptions,
        sendMessage:serviceImpl.SendMessage,
    })
l.start((err)=>{
    if(err===undefined){
        console.log("Server listening, v"+VERSION +` [${NAMED_PIPE_NAME}]`)//don't change, string is part of the protocol
    }
    else{
        console.log(err)
    }
})

process.stdin.on("data",data=>{
    if(data.toString().trim()=="q"){
        //todo: close server gracefully
        process.exit(0);
    }
})