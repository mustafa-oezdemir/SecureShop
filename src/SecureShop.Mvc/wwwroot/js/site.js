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
