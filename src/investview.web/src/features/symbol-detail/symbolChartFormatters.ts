export function formatChartPrice(value: number | undefined) {
  if (value == null) {
    return '-';
  }

  const displayValue = Math.abs(value) >= 1000 ? value / 1000 : value;
  return displayValue.toFixed(2);
}

export function formatCompactQuantity(value: number) {
  if (value >= 1_000_000) {
    return `${(value / 1_000_000).toFixed(3)}M`;
  }

  if (value >= 1_000) {
    return `${(value / 1_000).toFixed(3)}K`;
  }

  return value.toLocaleString('en-US');
}
