#!/usr/bin/env node
//@ts-check
import { NamedPipeServer } from "grpc-js-namedpipes";

import WAWebJS from "whatsapp-web.js";
import * as services from "./generated/WWebJS_grpc_pb";
import messages from "./generated/WWebJS_pb";
import { sendUnaryData, handleUnaryCall } from "@grpc/grpc-js";
const VERSION = require('../package.json').version;
import { WWebJSServiceImpl } from "./WWebJSService";
import { WWebJsServiceService } from "./generated/WWebJS";
import { createServer } from "net"
if (process.argv.length < 3) {
    throw new Error("expected at 1 or more arguments")
}
var NAMED_PIPE_NAME = process.argv[2];
console.log("node: " + process.version)
var shouldMonitor = process.argv.includes("--monitor")
const HEARTBEAT_INTV = 1000;//ms
const EXIT_MISSED_HEARTBEAT = 3;
const EXIT_SUCCESS = 0;
const EXIT_OTHER_ERROR = 0;
const HEARTBEAT_MAGIC_APPEND = "-mi6d693136"; //the heart beat server name is derived from the main server name by appending this magic sequence
const HEARTBEAT_MSG = "1"; //the expected message
var lastRecievedHeartBeatSignal = Date.now();
const devGetLastMessage: handleUnaryCall<proto.WWebJsService.DevGetLastMessageRequest, proto.WWebJsService.DevGetLastMessageResponse> =
    (call, callback) => {
        var response = new proto.WWebJsService.DevGetLastMessageResponse();
        var lastMsg = new proto.WWebJsService.Message();
        lastMsg.setBody("message from wds")

        response.setMessage(lastMsg)
        callback(null, response);
    }

var serviceImpl = new WWebJSServiceImpl({
    chromeExecutablePath: "null", headlessChromeMode: false,
    chromeArgs: [],
    userDataDir: "",
    verbose: true
});

//# monitoring the presence of the main process using heartbeat detection 
if (shouldMonitor) {
    console.log("starting heartbeat server")
    const heartBeatServer = createServer();//we use a dedicated named pipe server
    heartBeatServer.listen(`\\\\.\\pipe\\${NAMED_PIPE_NAME}${HEARTBEAT_MAGIC_APPEND}`, () => {
        lastRecievedHeartBeatSignal = Date.now();
    });
    heartBeatServer.once("connection", s => {
        console.log("h conn");
        s.on("data", (data) => {
            if (data.compare(Buffer.from(HEARTBEAT_MSG)) !== 0) {
                console.error("unexpected data: ", data)
                process.exit(EXIT_OTHER_ERROR)
            }
            else {
                lastRecievedHeartBeatSignal = Date.now();
            }
        })
    })
}
var l = new NamedPipeServer(NAMED_PIPE_NAME)

l.addServiceGrpcJs(WWebJsServiceService,
    {
        devGetLastMessage: serviceImpl.devGetLastMessage,
        devGetMessages: serviceImpl.devGetMessages,
        initClient: serviceImpl.initClient,
        sendMessage: serviceImpl.sendMessage,
        setOptions: serviceImpl.setOptions,
        ping: serviceImpl.ping,
        exit: serviceImpl.exit
    })
l.start((err) => {
    if (err === undefined) {
        console.log("Server listening, v" + VERSION + ` [${NAMED_PIPE_NAME}]`)//don't change, string is part of the protocol
        if (shouldMonitor) {
            lastRecievedHeartBeatSignal = Date.now();
            setInterval(() => {
                if ((Date.now() - lastRecievedHeartBeatSignal) > (2 * HEARTBEAT_INTV)) {
                    console.error("missed beat, closing...")
                    process.exit(EXIT_MISSED_HEARTBEAT)
                }
            }, HEARTBEAT_INTV);
        }
    }
    else {
        console.log(err)
    }
})

process.stdin.on("data", data => {
    if (data.toString().trim() == "q") {
        //todo: close server gracefully
        process.exit(EXIT_SUCCESS);
    }
})

