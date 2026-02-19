const { createApp, ref, onMounted } = Vue;

const app = createApp({
    setup() {
        // --- 1. 響應式變數定義 (Refs) ---
        const currentNumber = ref("---");
        const branchName = ref("總店服務中心");
        const connectionStatus = ref("連線中...");

        // 補齊遺漏的變數 [cite: 2026-02-19]
        const waitingCount = ref(0);
        const history = ref([]);
        const isFlashing = ref(false);

        // --- 2. 語音叫號邏輯 ---
        const speakNumber = (number) => {
            const message = new SpeechSynthesisUtterance(`請 ${number} 號到櫃檯`);
            message.lang = 'zh-TW';
            message.rate = 0.9;
            message.pitch = 1.0;
            message.volume = 1.0;
            window.speechSynthesis.speak(message);
        };

        // --- 3. SignalR 連線配置 ---
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7296/hubs/queue")
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .build();

        // 監聽：跳號通知 (包含人數更新) [cite: 2026-02-19]
        connection.on("ReceiveNewTicket", (ticket) => {
            console.log("收到跳號通知:", ticket);

            // A. 更新歷史紀錄：將舊號碼推入歷史
            if (currentNumber.value !== "---" && currentNumber.value !== ticket.ticketNumber) {
                history.value.unshift(currentNumber.value);
                if (history.value.length > 3) history.value.pop();
            }

            // B. 更新主顯示與等待人數 (注意 JSON 屬性為小寫) [cite: 2026-01-01]
            currentNumber.value = ticket.ticketNumber;
            waitingCount.value = ticket.waitingCount || 0;

            // C. 觸發閃爍動畫
            isFlashing.value = true;
            setTimeout(() => { isFlashing.value = false; }, 2000);

            // D. 執行語音播報
            speakNumber(ticket.ticketNumber);
        });

        // 監聽：單純人數更新 (取號時觸發) [cite: 2026-02-19]
        connection.on("UpdateWaitingCount", (count) => {
            console.log("收到人數更新通知:", count);
            waitingCount.value = count;
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

        // --- 4. 關鍵：必須回傳給 Template 才能顯示 ---
        return {
            currentNumber,
            branchName,
            connectionStatus,
            waitingCount,
            history,
            isFlashing
        };
    }
});

app.mount('#app');