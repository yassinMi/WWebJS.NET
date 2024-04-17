//@ts-check
const {NamedPipeServer} = require("grpc-js-namedpipes")
const WAWebJS = require("whatsapp-web.js")
const services = require("./generated/WWebJS_grpc_pb")
const messages = require("./generated/WWebJS_pb")
const VERSION = require('./package.json').version;
if(process.argv.length<3){
    throw new Error("expected at 1 or more arguments")
}
var NAMED_PIPE_NAME = process.argv[2];
console.log("node: "+process.version)
function getLastMessage(call,callback){
    var response = new proto.WWebJsService.GetLastMessageResponse();
    var lastMsg = new proto.WWebJsService.Message();
    lastMsg.setBody("message from wds")

    response.setMessage(lastMsg)
    callback(response);
}


var l = new NamedPipeServer(NAMED_PIPE_NAME)
l.addService(services.WWebJsServiceService, {getLastMessage:getLastMessage})
l.start((err)=>{
    if(err===undefined){
        console.log("Server listening, v"+VERSION +` [${NAMED_PIPE_NAME}]`)//don't change, string is part of the protocol
    }
    else{
        console.log(err)
    }
})