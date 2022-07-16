import { DaprServer, CommunicationProtocolEnum } from 'dapr-client';

const DAPR_HOST = process.env.DAPR_HOST || "http://localhost";
const DAPR_HTTP_PORT = process.env.DAPR_HTTP_PORT || "3501";
const SERVER_HOST = process.env.SERVER_HOST || "127.0.0.1";
const SERVER_PORT = process.env.SERVER_PORT || "5002";
const PUBSUB_NAME = process.env.PUBSUB_NAME || "notifications-pubsub";
const PUBSUB_TOPIC = process.env.PUBSUB_TOPIC || "notifications";

async function main() {
    const server = new DaprServer(
        SERVER_HOST,
        SERVER_PORT,
        DAPR_HOST,
        DAPR_HTTP_PORT,
        CommunicationProtocolEnum.HTTP);

    console.log(`Listening for subscriptions on ${SERVER_HOST}:${SERVER_PORT}. Pubsub name: '${PUBSUB_NAME}'. Topic: '${PUBSUB_TOPIC}'.`)

    await server.pubsub.subscribe(PUBSUB_NAME, PUBSUB_TOPIC, async (data) => {
        console.log("Notification received: " + JSON.stringify(data));
    });

    await server.start();
};

main().catch(e => console.error(e));