import express from 'express';

const port = 5004;

const app = express();
app.use(express.json());

app.post('/process-payment', (req, res) => {
  console.log(`Payment received: ${req.body.amount} ${req.body.currency}`)
  res.send({'message':'payment received!'});
});

app.listen(port, () => {
  return console.log(`Payment Service is listening at http://localhost:${port}`);
});