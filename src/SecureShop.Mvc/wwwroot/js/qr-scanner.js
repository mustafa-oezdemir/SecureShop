(() => {
    "use strict";

    const scanner = document.querySelector("[data-qr-scanner]");

    if (!scanner) {
        return;
    }

    const startButton = scanner.querySelector("[data-qr-scanner-start]");
    const stopButton = scanner.querySelector("[data-qr-scanner-stop]");
    const stage = scanner.querySelector("[data-qr-scanner-stage]");
    const video = scanner.querySelector("[data-qr-scanner-video]");
    const status = scanner.querySelector("[data-qr-scanner-status]");
    const upload = scanner.querySelector("[data-qr-scanner-upload]");
    let detector = null;
    let stream = null;
    let scanTimer = null;
    let isDetecting = false;

    const setStatus = (message, type = "info") => {
        status.textContent = message;
        status.className = `qr-scanner-status alert alert-${type}`;
    };

    const createDetector = async () => {
        if (!("BarcodeDetector" in window)) {
            throw new Error(
                "Bu tarayıcı QR algılamayı desteklemiyor. Telefonun ana kamera uygulamasıyla QR kodunu tarayın.");
        }

        const supportedFormats =
            await window.BarcodeDetector.getSupportedFormats();

        if (!supportedFormats.includes("qr_code")) {
            throw new Error(
                "Bu tarayıcı QR kod biçimini desteklemiyor.");
        }

        return new window.BarcodeDetector({
            formats: ["qr_code"]
        });
    };

    const stopCamera = () => {
        if (scanTimer !== null) {
            window.clearTimeout(scanTimer);
            scanTimer = null;
        }

        stream?.getTracks().forEach((track) => track.stop());
        stream = null;
        video.srcObject = null;
        stage.classList.add("d-none");
        startButton.classList.remove("d-none");
        stopButton.classList.add("d-none");
        isDetecting = false;
    };

    const openVerificationUrl = (rawValue) => {
        let scannedUrl;

        try {
            scannedUrl = new URL(rawValue);
        } catch {
            setStatus(
                "Okunan QR geçerli bir web adresi içermiyor.",
                "danger");
            return false;
        }

        const expectedPath = "/employee/orders/verify";
        const token = scannedUrl.searchParams.get("token");

        if (scannedUrl.origin !== window.location.origin
            || scannedUrl.pathname.toLowerCase()
                !== expectedPath.toLowerCase()
            || !token) {
            setStatus(
                "Bu QR SecureShop teslim doğrulama kodu değil.",
                "danger");
            return false;
        }

        stopCamera();
        setStatus(
            "QR bulundu. Güvenli doğrulama ekranı açılıyor.",
            "success");
        window.location.assign(
            `${expectedPath}?token=${encodeURIComponent(token)}`);
        return true;
    };

    const detectFromSource = async (source) => {
        if (!detector) {
            detector = await createDetector();
        }

        const barcodes = await detector.detect(source);

        for (const barcode of barcodes) {
            if (barcode.rawValue
                && openVerificationUrl(barcode.rawValue)) {
                return true;
            }
        }

        return false;
    };

    const scanFrame = async () => {
        if (!stream || isDetecting) {
            return;
        }

        isDetecting = true;

        try {
            await detectFromSource(video);
        } catch (error) {
            stopCamera();
            setStatus(
                error instanceof Error
                    ? error.message
                    : "QR taraması tamamlanamadı.",
                "danger");
            return;
        } finally {
            isDetecting = false;
        }

        if (stream) {
            scanTimer = window.setTimeout(scanFrame, 250);
        }
    };

    const startCamera = async () => {
        if (!window.isSecureContext) {
            setStatus(
                "Kamera yalnızca HTTPS bağlantısında kullanılabilir.",
                "danger");
            return;
        }

        if (!navigator.mediaDevices?.getUserMedia) {
            setStatus(
                "Bu tarayıcı kamera erişimini desteklemiyor.",
                "danger");
            return;
        }

        startButton.disabled = true;
        setStatus("Kamera izni bekleniyor…");

        try {
            detector = await createDetector();
            stream = await navigator.mediaDevices.getUserMedia({
                audio: false,
                video: {
                    facingMode: {
                        ideal: "environment"
                    },
                    width: {
                        ideal: 1280
                    },
                    height: {
                        ideal: 720
                    }
                }
            });
            video.srcObject = stream;
            await video.play();
            stage.classList.remove("d-none");
            startButton.classList.add("d-none");
            stopButton.classList.remove("d-none");
            setStatus(
                "Kamera açık. QR kodunu çerçevenin içine getirin.",
                "success");
            scanTimer = window.setTimeout(scanFrame, 150);
        } catch (error) {
            stopCamera();
            setStatus(
                error instanceof Error
                    ? error.message
                    : "Kamera başlatılamadı.",
                "danger");
        } finally {
            startButton.disabled = false;
        }
    };

    const scanUploadedImage = async (file) => {
        if (!file.type.startsWith("image/")) {
            setStatus("Lütfen bir görsel dosyası seçin.", "danger");
            return;
        }

        setStatus("QR fotoğrafı inceleniyor…");

        try {
            const image = await createImageBitmap(file);
            const found = await detectFromSource(image);
            image.close();

            if (!found) {
                setStatus(
                    "Fotoğrafta okunabilir bir QR kod bulunamadı.",
                    "warning");
            }
        } catch (error) {
            setStatus(
                error instanceof Error
                    ? error.message
                    : "QR fotoğrafı okunamadı.",
                "danger");
        }
    };

    startButton.addEventListener("click", () => {
        void startCamera();
    });
    stopButton.addEventListener("click", () => {
        stopCamera();
        setStatus("Kamera kapatıldı.");
    });
    upload.addEventListener("change", () => {
        const file = upload.files?.[0];
        if (file) {
            void scanUploadedImage(file);
        }
    });
    window.addEventListener("pagehide", stopCamera);
})();
