import { Events } from "whatsapp-web.js";
import { ClientEventType } from "./generated/WWebJS";

export function mapEventEnumToString(event: ClientEventType): string {
    switch (event) {
        case ClientEventType.AUTHENTICATED: return 'authenticated';
        case ClientEventType.AUTHENTICATION_FAILURE: return 'auth_failure';
        case ClientEventType.READY: return 'ready';
        case ClientEventType.MESSAGE_RECEIVED: return 'message';
        case ClientEventType.MESSAGE_CREATE: return 'message_create';
        case ClientEventType.MESSAGE_REVOKED_EVERYONE: return 'message_revoke_everyone';
        case ClientEventType.MESSAGE_REVOKED_ME: return 'message_revoke_me';
        case ClientEventType.MESSAGE_ACK: return 'message_ack';
        case ClientEventType.MESSAGE_EDIT: return 'message_edit';
        case ClientEventType.MEDIA_UPLOADED: return 'media_uploaded';
        case ClientEventType.CONTACT_CHANGED: return 'contact_changed';
        case ClientEventType.GROUP_JOIN: return 'group_join';
        case ClientEventType.GROUP_LEAVE: return 'group_leave';
        case ClientEventType.GROUP_ADMIN_CHANGED: return 'group_admin_changed';
        case ClientEventType.GROUP_MEMBERSHIP_REQUEST: return 'group_membership_request';
        case ClientEventType.GROUP_UPDATE: return 'group_update';
        case ClientEventType.QR_RECEIVED: return 'qr';
        case ClientEventType.LOADING_SCREEN: return 'loading_screen';
        case ClientEventType.DISCONNECTED: return 'disconnected';
        case ClientEventType.STATE_CHANGED: return 'change_state';
        case ClientEventType.BATTERY_CHANGED: return 'change_battery';
        case ClientEventType.REMOTE_SESSION_SAVED: return 'remote_session_saved';
        case ClientEventType.CALL: return 'call';
        default: throw new Error(`Unknown event: ${event}`);
    }
}

export function mapStringToEventEnum(eventStr: Events): ClientEventType {
    switch (eventStr) {
        case 'authenticated': return ClientEventType.AUTHENTICATED;
        case 'auth_failure': return ClientEventType.AUTHENTICATION_FAILURE;
        case 'ready': return ClientEventType.READY;
        case 'message': return ClientEventType.MESSAGE_RECEIVED;
        case 'message_create': return ClientEventType.MESSAGE_CREATE;
        case 'message_revoke_everyone': return ClientEventType.MESSAGE_REVOKED_EVERYONE;
        case 'message_revoke_me': return ClientEventType.MESSAGE_REVOKED_ME;
        case 'message_ack': return ClientEventType.MESSAGE_ACK;
        case 'message_edit': return ClientEventType.MESSAGE_EDIT;
        case 'media_uploaded': return ClientEventType.MEDIA_UPLOADED;
        case 'contact_changed': return ClientEventType.CONTACT_CHANGED;
        case 'group_join': return ClientEventType.GROUP_JOIN;
        case 'group_leave': return ClientEventType.GROUP_LEAVE;
        case 'group_admin_changed': return ClientEventType.GROUP_ADMIN_CHANGED;
        case 'group_membership_request': return ClientEventType.GROUP_MEMBERSHIP_REQUEST;
        case 'group_update': return ClientEventType.GROUP_UPDATE;
        case 'qr': return ClientEventType.QR_RECEIVED;
        case 'loading_screen': return ClientEventType.LOADING_SCREEN;
        case 'disconnected': return ClientEventType.DISCONNECTED;
        case 'change_state': return ClientEventType.STATE_CHANGED;
        case 'change_battery': return ClientEventType.BATTERY_CHANGED;
        case 'remote_session_saved': return ClientEventType.REMOTE_SESSION_SAVED;
        case 'call': return ClientEventType.CALL;
        default: throw new Error(`Unknown event string: ${eventStr}`);
    }
}
