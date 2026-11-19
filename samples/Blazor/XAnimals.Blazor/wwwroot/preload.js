// Loads images into the browser's cache, one at a time, so a page finds them there when it shows them.
export async function preload(urls) {
    for (const url of urls) {
        await new Promise(resolve => {
            const image = new Image();
            image.onload = image.onerror = resolve;
            image.src = url;
        });
    }
}
