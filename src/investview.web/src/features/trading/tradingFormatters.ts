const moneyFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 0,
  minimumFractionDigits: 0,
});

const quantityFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 0,
});

const priceFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 2,
  minimumFractionDigits: 2,
});

export function formatMoney(value: number | null | undefined) {
  return `${moneyFormatter.format(value ?? 0)} VND`;
}

export function formatQuantity(value: number | null | undefined) {
  return quantityFormatter.format(value ?? 0);
}

export function formatOrderPrice(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  return priceFormatter.format(value);
}
