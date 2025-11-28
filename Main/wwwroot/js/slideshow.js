
function startSlideshow(shots, token) {
    if (!shots || shots.length === 0) {
        console.warn("No shots available for slideshow.");
        return;
    }

    let currentIndex = 0;
    let isPlaying = true;
    let intervalId = null;
    let intervalTime = 3000; // 3 seconds

    // Create overlay elements
    const overlay = document.createElement('div');
    overlay.id = 'slideshow-overlay';
    overlay.style.position = 'fixed';
    overlay.style.top = '0';
    overlay.style.left = '0';
    overlay.style.width = '100%';
    overlay.style.height = '100%';
    overlay.style.backgroundColor = 'rgba(0, 0, 0, 0.95)';
    overlay.style.zIndex = '10000';
    overlay.style.display = 'flex';
    overlay.style.flexDirection = 'column';
    overlay.style.justifyContent = 'center';
    overlay.style.alignItems = 'center';
    overlay.style.backdropFilter = 'blur(10px)';
    overlay.style.opacity = '0';
    overlay.style.transition = 'opacity 0.3s ease-in-out';

    // Image container
    const imgContainer = document.createElement('div');
    imgContainer.style.position = 'relative';
    imgContainer.style.width = '100%';
    imgContainer.style.height = '100%';
    imgContainer.style.display = 'flex';
    imgContainer.style.justifyContent = 'center';
    imgContainer.style.alignItems = 'center';

    const img = document.createElement('img');
    img.style.maxWidth = '95%';
    img.style.maxHeight = '95%';
    img.style.objectFit = 'contain';
    img.style.boxShadow = '0 0 30px rgba(0,0,0,0.8)';
    img.style.transition = 'opacity 0.5s ease-in-out, transform 0.5s ease-in-out';
    img.style.opacity = '0';
    imgContainer.appendChild(img);

    // Controls container
    const controls = document.createElement('div');
    controls.style.position = 'absolute';
    controls.style.bottom = '30px';
    controls.style.display = 'flex';
    controls.style.gap = '15px';
    controls.style.zIndex = '10001';
    controls.style.padding = '10px 20px';
    controls.style.borderRadius = '30px';
    controls.style.backgroundColor = 'rgba(255, 255, 255, 0.1)';
    controls.style.backdropFilter = 'blur(5px)';
    controls.style.alignItems = 'center';

    const createBtn = (text, icon, onClick) => {
        const btn = document.createElement('button');
        btn.innerHTML = icon || text;
        btn.title = text;
        btn.onclick = (e) => {
            e.stopPropagation();
            onClick();
        };
        btn.style.width = '40px';
        btn.style.height = '40px';
        btn.style.border = 'none';
        btn.style.borderRadius = '50%';
        btn.style.backgroundColor = 'transparent';
        btn.style.color = 'white';
        btn.style.fontSize = '20px';
        btn.style.cursor = 'pointer';
        btn.style.display = 'flex';
        btn.style.justifyContent = 'center';
        btn.style.alignItems = 'center';
        btn.style.transition = 'background-color 0.2s, transform 0.2s';

        btn.onmouseover = () => {
            btn.style.backgroundColor = 'rgba(255, 255, 255, 0.2)';
            btn.style.transform = 'scale(1.1)';
        };
        btn.onmouseout = () => {
            btn.style.backgroundColor = 'transparent';
            btn.style.transform = 'scale(1)';
        };
        return btn;
    };

    // Icons
    const prevIcon = '&#10094;';
    const nextIcon = '&#10095;';
    const playIcon = '&#9658;';
    const pauseIcon = '&#10074;&#10074;';
    const closeIcon = '&#10005;';
    const slowerIcon = '&#128034;'; // Turtle
    const fasterIcon = '&#128007;'; // Rabbit

    // Speed Display
    const speedDisplay = document.createElement('span');
    speedDisplay.style.color = 'white';
    speedDisplay.style.fontSize = '14px';
    speedDisplay.style.minWidth = '40px';
    speedDisplay.style.textAlign = 'center';
    speedDisplay.innerText = (intervalTime / 1000) + 's';

    const updateSpeed = (delta) => {
        let newTime = intervalTime + delta;
        if (newTime < 1000) newTime = 1000; // Min 1 sec
        if (newTime > 10000) newTime = 10000; // Max 10 sec

        if (newTime !== intervalTime) {
            intervalTime = newTime;
            speedDisplay.innerText = (intervalTime / 1000) + 's';
            if (isPlaying) {
                stopAutoPlay();
                startAutoPlay();
            }
        }
    };

    const slowerBtn = createBtn('Slower', slowerIcon, () => updateSpeed(1000));
    const prevBtn = createBtn('Prev', prevIcon, () => {
        stopAutoPlay();
        showSlide(currentIndex - 1);
    });

    const playPauseBtn = createBtn('Pause', pauseIcon, () => {
        if (isPlaying) {
            stopAutoPlay();
        } else {
            startAutoPlay();
        }
    });

    const nextBtn = createBtn('Next', nextIcon, () => {
        stopAutoPlay();
        showSlide(currentIndex + 1);
    });

    const fasterBtn = createBtn('Faster', fasterIcon, () => updateSpeed(-1000));

    const closeBtn = createBtn('Close', closeIcon, closeSlideshow);

    controls.appendChild(slowerBtn);
    controls.appendChild(prevBtn);
    controls.appendChild(playPauseBtn);
    controls.appendChild(nextBtn);
    controls.appendChild(fasterBtn);
    controls.appendChild(speedDisplay);
    controls.appendChild(closeBtn);

    overlay.appendChild(imgContainer);
    overlay.appendChild(controls);
    document.body.appendChild(overlay);

    // Fade in overlay
    requestAnimationFrame(() => {
        overlay.style.opacity = '1';
    });

    function showSlide(index) {
        if (index < 0) index = shots.length - 1;
        if (index >= shots.length) index = 0;
        currentIndex = index;

        const shot = shots[currentIndex];
        // Normalize properties (handle PascalCase from Newtonsoft and camelCase from System.Text.Json)
        const shotId = shot.ShotId || shot.shotId;
        const flip = shot.Flip !== undefined ? shot.Flip : shot.flip;
        const rotate = shot.Rotate !== undefined ? shot.Rotate : shot.rotate;

        // Use /shot for high quality
        const src = `/shot?id=${shotId}&flip=${flip}&rotate=${rotate}&token=${token}`;

        // Preload next image
        const nextIndex = (currentIndex + 1) % shots.length;
        const nextShot = shots[nextIndex];
        const nextShotId = nextShot.ShotId || nextShot.shotId;
        const nextFlip = nextShot.Flip !== undefined ? nextShot.Flip : nextShot.flip;
        const nextRotate = nextShot.Rotate !== undefined ? nextShot.Rotate : nextShot.rotate;
        const nextSrc = `/shot?id=${nextShotId}&flip=${nextFlip}&rotate=${nextRotate}&token=${token}`;
        const preloadImg = new Image();
        preloadImg.src = nextSrc;

        img.style.opacity = '0';
        img.style.transform = 'scale(0.95)';

        setTimeout(() => {
            img.src = src;
            img.onload = () => {
                img.style.opacity = '1';
                img.style.transform = 'scale(1)';
            };
        }, 300);
    }

    function startAutoPlay() {
        isPlaying = true;
        playPauseBtn.innerHTML = pauseIcon;
        intervalId = setInterval(() => {
            showSlide(currentIndex + 1);
        }, intervalTime);
    }

    function stopAutoPlay() {
        isPlaying = false;
        playPauseBtn.innerHTML = playIcon;
        clearInterval(intervalId);
    }

    function closeSlideshow() {
        stopAutoPlay();
        overlay.style.opacity = '0';
        setTimeout(() => {
            if (document.body.contains(overlay)) {
                document.body.removeChild(overlay);
            }
        }, 300);
        document.removeEventListener('keydown', handleKeydown);
    }

    function handleKeydown(e) {
        if (e.key === 'ArrowLeft') {
            stopAutoPlay();
            showSlide(currentIndex - 1);
        } else if (e.key === 'ArrowRight') {
            stopAutoPlay();
            showSlide(currentIndex + 1);
        } else if (e.key === 'Escape') {
            closeSlideshow();
        } else if (e.key === ' ') { // Spacebar
            e.preventDefault();
            if (isPlaying) stopAutoPlay();
            else startAutoPlay();
        } else if (e.key === '+' || e.key === '=') {
            updateSpeed(-1000);
        } else if (e.key === '-' || e.key === '_') {
            updateSpeed(1000);
        }
    }

    document.addEventListener('keydown', handleKeydown);

    // Start
    showSlide(0);
    startAutoPlay();
}
