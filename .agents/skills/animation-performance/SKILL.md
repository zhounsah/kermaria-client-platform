---
name: animation-performance
description: Mesurer et contenir le coût des animations du WebPortal.
---

# Budget mouvement

Préférer les propriétés compositées et éviter les boucles permanentes sans fonction. Garder les médias dimensionnés, locaux et adaptés au viewport. Comparer desktop, mobile et mouvement réduit ; surveiller LCP, CLS, débordement et erreurs console. Ne pas introduire GSAP, Lenis ou Three.js tant qu'un besoin mesuré ne dépasse pas les capacités CSS/WAAPI du projet.
