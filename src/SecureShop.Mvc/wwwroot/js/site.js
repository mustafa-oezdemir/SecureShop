// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.querySelectorAll("[data-product-gallery]").forEach((gallery) => {
    const mainImage = gallery.querySelector("[data-gallery-main-image]");
    const thumbnails = gallery.querySelectorAll("[data-gallery-thumbnail]");

    if (!mainImage || thumbnails.length === 0) {
        return;
    }

    thumbnails.forEach((thumbnail) => {
        thumbnail.addEventListener("click", () => {
            const imageUrl = thumbnail.dataset.imageUrl;
            const imageAlt = thumbnail.dataset.imageAlt;

            if (!imageUrl) {
                return;
            }

            mainImage.src = imageUrl;
            mainImage.alt = imageAlt ?? "Ürün fotoğrafı";

            thumbnails.forEach((currentThumbnail) => {
                const isActive = currentThumbnail === thumbnail;

                currentThumbnail.classList.toggle("is-active", isActive);
                currentThumbnail.setAttribute(
                    "aria-selected",
                    isActive ? "true" : "false");
            });
        });
    });
});

document.querySelectorAll("[data-product-image-input]").forEach((input) => {
    const preview = document.querySelector("[data-product-image-preview]");

    if (!preview) {
        return;
    }

    input.addEventListener("change", () => {
        preview.replaceChildren();

        Array.from(input.files ?? []).slice(0, 10).forEach((file, index) => {
            const item = document.createElement("div");
            const image = document.createElement("img");
            const label = document.createElement("span");

            item.className = "product-upload-preview-item";
            image.src = URL.createObjectURL(file);
            image.alt = `Seçilen fotoğraf ${index + 1}`;
            image.addEventListener("load", () => URL.revokeObjectURL(image.src));
            label.textContent = index === 0 ? "Ana fotoğraf" : `${index + 1}. fotoğraf`;

            item.append(image, label);
            preview.append(item);
        });
    });
});
