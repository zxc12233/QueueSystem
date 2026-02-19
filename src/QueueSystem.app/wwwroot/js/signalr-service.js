const { createApp, ref, onMounted } = Vue;

const app = createApp({
    setup() {
        const currentNumber = ref("---");
        const branchName = ref("總店服務中心");
        const connectionStatus = ref("連線中...");

        // --- 語音叫號邏輯 ---
        const speakNumber = (number) => {
            // 1. 建立語音請求物件
            const message = new SpeechSynthesisUtterance(`請 ${number} 號到櫃檯`);

            // 2. 設定語音參數
            message.lang = 'zh-TW';     // 設定語言：台灣繁體中文
            message.rate = 0.9;         // 語速 (0.1 ~ 10)，略慢一點比較清晰
            message.pitch = 1.0;        // 音調 (0 ~ 2)
            message.volume = 1.0;       // 音量 (0 ~ 1)

            // 3. 執行播報 (瀏覽器會自動排隊 queue 訊息，不怕連點)
            window.speechSynthesis.speak(message);
        };

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7296/hubs/queue")
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .build();

        connection.on("ReceiveNewTicket", (ticket) => {
            console.log("收到跳號通知:", ticket);
            currentNumber.value = ticket.ticketNumber;

            // 【核心觸發】號碼更新時同時播報語音
            speakNumber(ticket.ticketNumber);
        });

        const startConnection = async () => {
            try {
                await connection.start();
                connectionStatus.value = "已連線";
            } catch (err) {
                connectionStatus.value = "連線失敗，重試中...";
                setTimeout(startConnection, 5000);
            }
        };

        onMounted(() => {
            startConnection();
        });

        return { currentNumber, branchName, connectionStatus };
    }
});

app.mount('#app');