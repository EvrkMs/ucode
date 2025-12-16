FROM node:20-alpine

WORKDIR /app

COPY frontend-ejs/package.json ./

RUN npm install --production && npm cache clean --force

COPY frontend-ejs/. .

ENV PORT=3000
EXPOSE 3000

CMD ["node", "server.js"]
