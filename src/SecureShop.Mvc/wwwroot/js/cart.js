(() => {
    "use strict";

    const page = document.querySelector("[data-cart-page]");

    if (!page) {
        return;
    }

    const currencyFormatter = new Intl.NumberFormat("tr-TR", {
        style: "currency",
        currency: "EUR"
    });
    const errorBox = page.querySelector("[data-cart-error]");
    const liveStatus = page.querySelector("[data-cart-live-status]");
    const pendingUpdates = new WeakMap();

    const formatCurrency = (value) =>
        currencyFormatter.format(Number(value));

    const announce = (message) => {
        if (!liveStatus) {
            return;
        }

        liveStatus.textContent = "";
        window.setTimeout(() => {
            liveStatus.textContent = message;
        }, 30);
    };

    const showError = (message) => {
        if (!errorBox) {
            return;
        }

        errorBox.textContent = message;
        errorBox.classList.remove("d-none");
        announce(message);
    };

    const clearError = () => {
        if (!errorBox) {
            return;
        }

        errorBox.textContent = "";
        errorBox.classList.add("d-none");
    };

    const setFormBusy = (form, isBusy) => {
        form.classList.toggle("is-updating", isBusy);
        form.setAttribute("aria-busy", isBusy ? "true" : "false");

        form.querySelectorAll("button, input").forEach((control) => {
            if (isBusy) {
                control.dataset.wasDisabled = control.disabled ? "true" : "false";
                control.disabled = true;
            } else {
                control.disabled = control.dataset.wasDisabled === "true";
                delete control.dataset.wasDisabled;
            }
        });
    };

    const updateButtons = (form) => {
        const input = form.querySelector("[data-cart-quantity]");
        const decrement = form.querySelector("[data-quantity-decrement]");
        const increment = form.querySelector("[data-quantity-increment]");

        if (!input || !decrement || !increment || form.classList.contains("is-updating")) {
            return;
        }

        const value = Number(input.value);
        const minimum = Number(input.min || 1);
        const maximum = Number(input.max || 99);

        decrement.disabled = value <= minimum;
        increment.disabled = value >= maximum;
    };

    const applyCart = (cart) => {
        cart.items.forEach((item) => {
            const card = page.querySelector(
                `[data-cart-item][data-item-id="${item.id}"]`);

            if (!card) {
                return;
            }

            const input = card.querySelector("[data-cart-quantity]");
            const lineTotal = card.querySelector("[data-line-total]");

            if (input) {
                input.value = item.quantity;
                input.dataset.committedValue = item.quantity;
            }

            if (lineTotal) {
                lineTotal.textContent = formatCurrency(item.lineTotal);
            }

            const form = card.querySelector("[data-cart-quantity-form]");
            if (form) {
                updateButtons(form);
            }
        });

        page.querySelectorAll("[data-cart-total-quantity], [data-cart-heading-quantity]")
            .forEach((element) => {
                element.textContent = cart.totalQuantity;
            });
        page.querySelectorAll("[data-cart-subtotal], [data-cart-total]")
            .forEach((element) => {
                element.textContent = formatCurrency(cart.totalAmount);
            });
        document.querySelectorAll("[data-cart-nav-count]")
            .forEach((element) => {
                element.textContent = cart.totalQuantity;
                const link = element.closest("a");
                if (link) {
                    link.setAttribute(
                        "aria-label",
                        `Sepetim, ${cart.totalQuantity} ürün`);
                }
            });
    };

    const readError = async (response) => {
        try {
            const payload = await response.json();
            return payload.error
                ?? payload.detail
                ?? "Sepet güncellenemedi.";
        } catch {
            return "Sepet güncellenemedi.";
        }
    };

    const updateQuantity = async (form) => {
        const input = form.querySelector("[data-cart-quantity]");

        if (!input || form.classList.contains("is-updating")) {
            return;
        }

        const minimum = Number(input.min || 1);
        const maximum = Number(input.max || 99);
        const requestedQuantity = Number(input.value);
        const committedQuantity = Number(input.dataset.committedValue);

        if (!Number.isInteger(requestedQuantity)
            || requestedQuantity < minimum
            || requestedQuantity > maximum) {
            input.value = committedQuantity;
            updateButtons(form);
            showError(`Adet ${minimum} ile ${maximum} arasında olmalıdır.`);
            return;
        }

        if (requestedQuantity === committedQuantity) {
            updateButtons(form);
            return;
        }

        clearError();
        const formData = new FormData(form);
        setFormBusy(form, true);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(await readError(response));
            }

            const cart = await response.json();
            applyCart(cart);
            announce(`Ürün adedi ${requestedQuantity} olarak güncellendi.`);
        } catch (error) {
            input.value = committedQuantity;
            showError(
                error instanceof Error
                    ? error.message
                    : "Sepet güncellenemedi.");
        } finally {
            setFormBusy(form, false);
            updateButtons(form);
        }
    };

    const scheduleUpdate = (form, delay = 400) => {
        const currentTimer = pendingUpdates.get(form);
        if (currentTimer) {
            window.clearTimeout(currentTimer);
        }

        pendingUpdates.set(
            form,
            window.setTimeout(() => {
                pendingUpdates.delete(form);
                void updateQuantity(form);
            }, delay));
    };

    page.querySelectorAll("[data-cart-quantity-form]").forEach((form) => {
        const input = form.querySelector("[data-cart-quantity]");
        const decrement = form.querySelector("[data-quantity-decrement]");
        const increment = form.querySelector("[data-quantity-increment]");

        if (!input || !decrement || !increment) {
            return;
        }

        form.addEventListener("submit", (event) => {
            event.preventDefault();
            scheduleUpdate(form, 0);
        });

        input.addEventListener("input", () => {
            updateButtons(form);
            scheduleUpdate(form);
        });

        input.addEventListener("change", () => {
            scheduleUpdate(form, 0);
        });

        decrement.addEventListener("click", () => {
            input.stepDown();
            updateButtons(form);
            scheduleUpdate(form, 0);
        });

        increment.addEventListener("click", () => {
            input.stepUp();
            updateButtons(form);
            scheduleUpdate(form, 0);
        });

        updateButtons(form);
    });
})();
