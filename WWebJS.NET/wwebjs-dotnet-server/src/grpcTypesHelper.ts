import { Events } from "whatsapp-web.js";

export function mapEventEnumToString(event: proto.WWebJsService.ClientEventType): string {
    switch (event) {
        case proto.WWebJsService.ClientEventType.AUTHENTICATED: return 'authenticated';
        case proto.WWebJsService.ClientEventType.AUTHENTICATION_FAILURE: return 'auth_failure';
        case proto.WWebJsService.ClientEventType.READY: return 'ready';
        case proto.WWebJsService.ClientEventType.MESSAGE_RECEIVED: return 'message';
        case proto.WWebJsService.ClientEventType.MESSAGE_CREATE: return 'message_create';
        case proto.WWebJsService.ClientEventType.MESSAGE_REVOKED_EVERYONE: return 'message_revoke_everyone';
        case proto.WWebJsService.ClientEventType.MESSAGE_REVOKED_ME: return 'message_revoke_me';
        case proto.WWebJsService.ClientEventType.MESSAGE_ACK: return 'message_ack';
        case proto.WWebJsService.ClientEventType.MESSAGE_EDIT: return 'message_edit';
        case proto.WWebJsService.ClientEventType.MEDIA_UPLOADED: return 'media_uploaded';
        case proto.WWebJsService.ClientEventType.CONTACT_CHANGED: return 'contact_changed';
        case proto.WWebJsService.ClientEventType.GROUP_JOIN: return 'group_join';
        case proto.WWebJsService.ClientEventType.GROUP_LEAVE: return 'group_leave';
        case proto.WWebJsService.ClientEventType.GROUP_ADMIN_CHANGED: return 'group_admin_changed';
        case proto.WWebJsService.ClientEventType.GROUP_MEMBERSHIP_REQUEST: return 'group_membership_request';
        case proto.WWebJsService.ClientEventType.GROUP_UPDATE: return 'group_update';
        case proto.WWebJsService.ClientEventType.QR_RECEIVED: return 'qr';
        case proto.WWebJsService.ClientEventType.LOADING_SCREEN: return 'loading_screen';
        case proto.WWebJsService.ClientEventType.DISCONNECTED: return 'disconnected';
        case proto.WWebJsService.ClientEventType.STATE_CHANGED: return 'change_state';
        case proto.WWebJsService.ClientEventType.BATTERY_CHANGED: return 'change_battery';
        case proto.WWebJsService.ClientEventType.REMOTE_SESSION_SAVED: return 'remote_session_saved';
        case proto.WWebJsService.ClientEventType.CALL: return 'call';
        default: throw new Error(`Unknown event: ${event}`);
    }
}

export function mapStringToEventEnum(eventStr: Events): proto.WWebJsService.ClientEventType {
    switch (eventStr) {
        case 'authenticated': return proto.WWebJsService.ClientEventType.AUTHENTICATED;
        case 'auth_failure': return proto.WWebJsService.ClientEventType.AUTHENTICATION_FAILURE;
        case 'ready': return proto.WWebJsService.ClientEventType.READY;
        case 'message': return proto.WWebJsService.ClientEventType.MESSAGE_RECEIVED;
        case 'message_create': return proto.WWebJsService.ClientEventType.MESSAGE_CREATE;
        case 'message_revoke_everyone': return proto.WWebJsService.ClientEventType.MESSAGE_REVOKED_EVERYONE;
        case 'message_revoke_me': return proto.WWebJsService.ClientEventType.MESSAGE_REVOKED_ME;
        case 'message_ack': return proto.WWebJsService.ClientEventType.MESSAGE_ACK;
        case 'message_edit': return proto.WWebJsService.ClientEventType.MESSAGE_EDIT;
        case 'media_uploaded': return proto.WWebJsService.ClientEventType.MEDIA_UPLOADED;
        case 'contact_changed': return proto.WWebJsService.ClientEventType.CONTACT_CHANGED;
        case 'group_join': return proto.WWebJsService.ClientEventType.GROUP_JOIN;
        case 'group_leave': return proto.WWebJsService.ClientEventType.GROUP_LEAVE;
        case 'group_admin_changed': return proto.WWebJsService.ClientEventType.GROUP_ADMIN_CHANGED;
        case 'group_membership_request': return proto.WWebJsService.ClientEventType.GROUP_MEMBERSHIP_REQUEST;
        case 'group_update': return proto.WWebJsService.ClientEventType.GROUP_UPDATE;
        case 'qr': return proto.WWebJsService.ClientEventType.QR_RECEIVED;
        case 'loading_screen': return proto.WWebJsService.ClientEventType.LOADING_SCREEN;
        case 'disconnected': return proto.WWebJsService.ClientEventType.DISCONNECTED;
        case 'change_state': return proto.WWebJsService.ClientEventType.STATE_CHANGED;
        case 'change_battery': return proto.WWebJsService.ClientEventType.BATTERY_CHANGED;
        case 'remote_session_saved': return proto.WWebJsService.ClientEventType.REMOTE_SESSION_SAVED;
        case 'call': return proto.WWebJsService.ClientEventType.CALL;
        default: throw new Error(`Unknown event string: ${eventStr}`);
    }
}
