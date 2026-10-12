// Presentation styles; inserted by the owning UI component.
export const friendStyles = `
#arena-friends {
  position: fixed;
  inset: 5vh 8vw;
  z-index: 8;
  padding: 22px;
  border: 1px solid #9d875a;
  border-radius: 14px;
  background: radial-gradient(ellipse at top, #282641, #0b101d 75%);
  box-shadow: 0 0 0 100vmax #02050bd9;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
#arena-friends h2 {
  font:
    26px Georgia,
    serif;
  letter-spacing: 4px;
  color: #e8cf90;
  margin: 0;
}
#arena-friends h3 {
  font-size: 13px;
  letter-spacing: 2px;
  color: #d3c293;
}
#arena-friends .friends-body {
  overflow: auto;
  min-height: 70px;
  flex: 1;
}
#arena-friends .friend-row {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
  border-bottom: 1px solid #38394b;
  padding: 10px 2px;
}
#arena-friends .friend-row strong {
  flex: 1;
  min-width: 120px;
}
#arena-friends button {
  font-size: 12px;
  padding: 8px 12px;
  border-color: #a68c54;
  background: #252333;
  color: #ecd59e;
  border-radius: 3px;
}
#arena-friends .friend-note {
  color: #93a9bf;
  font-size: 12px;
}
#arena-friends .friend-state {
  color: #a4dfca;
  font-size: 13px;
  margin: 0;
}
#arena-friends form {
  display: flex;
  gap: 10px;
  align-items: end;
  flex-wrap: wrap;
}
#arena-friends label {
  flex: 1;
  margin: 0;
}
#arena-friends input {
  max-width: 400px;
}
`;
