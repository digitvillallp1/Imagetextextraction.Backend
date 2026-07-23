# VPS Deployment Guide

1. Clone both repositories into the same main folder on your VPS:
   ```bash
   git clone https://github.com/digitvillallp1/Imagetextextraction.Frontend.git
   git clone https://github.com/digitvillallp1/Imagetextextraction.Backend.git
   ```

2. Navigate to this deployment folder:
   ```bash
   cd Imagetextextraction.Backend/VPS_Deploy
   ```

3. Create your `.env` file for the API key:
   ```bash
   nano .env
   ```
   Add the following inside:
   ```env
   DB_PASSWORD=postgres123
   GEMINI_API_KEY=your_actual_api_key_here
   ```
   Save and exit (Ctrl+X, Y, Enter).

4. Run the Docker containers:
   ```bash
   docker-compose up -d --build
   ```

5. Your website is now live! Simply enter your VPS IP address or Domain Name in the browser.
